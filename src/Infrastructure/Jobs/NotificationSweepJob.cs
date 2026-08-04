using System.Globalization;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RemSolution.Application.Common.Features;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Notifications;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using RemSolution.Infrastructure.Data;

namespace RemSolution.Infrastructure.Jobs;

/// <summary>
/// The recurring pass that looks for things worth telling somebody about: car
/// costs coming due, hires that have not come back, holds about to start, and the
/// reminders clients are owed before a rental begins and ends.
/// <para>
/// Detection lives here; deciding who hears it, and sending, is
/// <see cref="INotificationService"/>'s. This job only asks questions of the
/// database and hands over findings.
/// </para>
/// <para>
/// Runs with no HTTP context and must cover every agency, so — exactly like
/// <see cref="ReservationExpiryJob"/> — it enumerates agencies (Agency is not
/// tenant-scoped) and processes each under its own <see cref="AmbientTenant"/>
/// push, rather than bypassing the tenant query filter. Each agency's alerts are
/// therefore produced by the same filters a request would see.
/// </para>
/// <para>
/// Safe to run as often as you like: every notification carries a dedup key, so
/// re-running inside the same time bucket tells nobody anything twice.
/// </para>
/// </summary>
public sealed class NotificationSweepJob
{
    // The concrete context: the client reminders need the linked portal account's
    // chosen language, which lives on the Identity user rather than in the domain.
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notifications;
    private readonly IAgencySettingsProvider _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NotificationSweepJob> _logger;

    public NotificationSweepJob(
        ApplicationDbContext context,
        INotificationService notifications,
        IAgencySettingsProvider settings,
        TimeProvider timeProvider,
        ILogger<NotificationSweepJob> logger)
    {
        _context = context;
        _notifications = notifications;
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task RunAsync()
    {
        var now = _timeProvider.GetUtcNow();
        var agencyIds = await _context.Agencies.Select(a => a.Id).ToListAsync();

        foreach (var agencyId in agencyIds)
        {
            using var _ = AmbientTenant.Push(agencyId);

            try
            {
                await SweepAgencyAsync(agencyId, now, CancellationToken.None);
            }
            catch (Exception exception)
            {
                // One agency's bad data (or a mail server it alone uses) must not
                // cost every other agency its alerts for the hour.
                _logger.LogError(
                    exception, "The notification sweep failed for agency {AgencyId}.", agencyId);
            }
        }
    }

    private async Task SweepAgencyAsync(
        int agencyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var features = await AgencyFeatureResolver.GetEnabledFeaturesAsync(
            _context, agencyId, now, cancellationToken);

        // Not subscribed to notifications: nothing is produced, so switching the
        // feature on later starts from a clean inbox rather than a backlog.
        if (!features.Contains(FeatureFlags.Notifications))
        {
            return;
        }

        var settings = await _settings.GetAsync(agencyId, cancellationToken);
        var utcNow = now.UtcDateTime;

        // Each category is additionally gated on the module it is about: an alert
        // that a hire is late is useless to an agency that does not have the
        // rentings module, and its recipients could not open the link anyway.
        if (features.Contains(FeatureFlags.Expenses) && features.Contains(FeatureFlags.Cars))
        {
            await SweepCarExpensesAsync(settings, utcNow, cancellationToken);
        }

        if (features.Contains(FeatureFlags.Rentings))
        {
            await SweepOverdueRentingsAsync(utcNow, cancellationToken);
        }

        if (features.Contains(FeatureFlags.Reservations))
        {
            await SweepUpcomingReservationsAsync(settings, utcNow, cancellationToken);
        }

        if (settings.NotifyClientsByEmail)
        {
            await SweepClientRemindersAsync(settings, utcNow, features, cancellationToken);
        }
    }

    // -----------------------------------------------------------------------
    // Fleet: maintenance and papers, as recurring expense types coming due.
    // -----------------------------------------------------------------------

    private async Task SweepCarExpensesAsync(
        AgencySettingsSnapshot settings, DateTime now, CancellationToken cancellationToken)
    {
        // The agency's own recurrence rules. A type flagged for notification with
        // neither interval set has nothing to compute from, so it is excluded
        // here rather than evaluated to null car by car.
        var types = await _context.ExpenseTypes
            .AsNoTracking()
            .Where(t => t.IsActive && t.WithNotif
                        && (t.AfterMonth > 0 || t.AfterKilometer > 0))
            .Select(t => new { t.Id, t.Name, t.AfterMonth, t.AfterKilometer })
            .ToListAsync(cancellationToken);

        if (types.Count == 0)
        {
            return;
        }

        var typeIds = types.Select(t => t.Id).ToList();

        var cars = await _context.Cars
            .AsNoTracking()
            // A retired car is not maintained. Cars already in Maintenance stay
            // in: that is often exactly why they are off the road.
            .Where(c => c.Status != CarStatus.Inactive)
            .Select(c => new
            {
                c.Id,
                c.Matricule,
                c.Mileage,
                ModelName = c.Model != null ? c.Model.Name : null,
            })
            .ToListAsync(cancellationToken);

        var carsById = cars.ToDictionary(c => c.Id);

        // The baseline for each (car, type): when this work was last booked, and
        // the odometer it was booked at.
        //
        // MAX on both columns rather than "the values of the latest row": an
        // odometer only moves forward (see Car.RecordOdometer), so the highest
        // reading recorded for a type IS the last time it was done — and taking
        // the maximum also lets an older expense that does carry a reading serve
        // as the distance baseline when the newest one was recorded without.
        var baselines = await _context.Expenses
            .AsNoTracking()
            .Where(e => typeIds.Contains(e.ExpenseTypeId))
            .GroupBy(e => new { e.CarId, e.ExpenseTypeId })
            .Select(g => new
            {
                g.Key.CarId,
                g.Key.ExpenseTypeId,
                LastExpenseOn = g.Max(e => e.ExpenseDate),
                LastMileage = g.Max(e => e.Mileage),
            })
            .ToListAsync(cancellationToken);

        foreach (var baseline in baselines)
        {
            if (!carsById.TryGetValue(baseline.CarId, out var car))
            {
                continue;
            }

            var type = types.FirstOrDefault(t => t.Id == baseline.ExpenseTypeId);

            if (type is null)
            {
                continue;
            }

            var due = ExpenseDueCalculator.Evaluate(
                type.AfterMonth,
                type.AfterKilometer,
                baseline.LastExpenseOn,
                baseline.LastMileage,
                car.Mileage,
                now,
                settings.ExpenseDueLeadDays,
                settings.ExpenseDueLeadKilometers);

            if (due is null)
            {
                continue;
            }

            var args = new NotificationArgs()
                .Set("car", CarLabel(car.ModelName, car.Matricule))
                .Set("type", type.Name)
                .Set("days", due.Days)
                .Set("mileage", car.Mileage?.ToString(CultureInfo.InvariantCulture))
                .SetDate("dueDate", due.DueOn);

            if (due.DueAtKilometers is int dueAt)
            {
                args.Set("dueKm", dueAt).Set("km", due.Kilometers ?? 0);
            }

            await _notifications.NotifyStaffAsync(
                new StaffNotification(
                    NotificationKind.CarExpenseDue,
                    due.MessageKey,
                    // Whoever books the agency's costs is who acts on this.
                    Permissions.ExpenseRead,
                    NotificationSubject.Car,
                    car.Id,
                    $"/car/{car.Id}",
                    args,
                    // The expense type is part of the identity — two types can be
                    // due on the same car — and the week bucket is the nagging
                    // rate: still due next week, said again. A due that tips over
                    // into overdue changes the message key, which is itself part
                    // of the key, so that is reported at once rather than waiting.
                    DedupToken: $"x{type.Id}|{WeekBucket(now)}"),
                cancellationToken);
        }
    }

    // -----------------------------------------------------------------------
    // Bookings the agency needs to chase.
    // -----------------------------------------------------------------------

    private async Task SweepOverdueRentingsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var overdue = await _context.Rentings
            .AsNoTracking()
            .Where(r => r.RentingState == RentingState.InProgress
                        && r.EndDate != null
                        && r.EndDate < now)
            .Select(r => new
            {
                r.Id,
                r.EndDate,
                r.ClientId,
                ClientFirstName = r.Client != null ? r.Client.FirstName : null,
                ClientLastName = r.Client != null ? r.Client.LastName : null,
                Matricule = r.Car != null ? r.Car.Matricule : null,
                ModelName = r.Car != null && r.Car.Model != null ? r.Car.Model.Name : null,
            })
            .ToListAsync(cancellationToken);

        foreach (var renting in overdue)
        {
            var days = WholeDaysBetween(renting.EndDate!.Value, now);

            var args = new NotificationArgs()
                .Set("car", CarLabel(renting.ModelName, renting.Matricule))
                .Set("client", PersonLabel(renting.ClientFirstName, renting.ClientLastName))
                .Set("days", days)
                .SetDate("endDate", renting.EndDate);

            await _notifications.NotifyStaffAsync(
                new StaffNotification(
                    NotificationKind.RentingOverdue,
                    NotificationMessages.RentingOverdue,
                    Permissions.RentingRead,
                    NotificationSubject.Renting,
                    renting.Id,
                    $"/renting/{renting.Id}",
                    args,
                    // Daily: a car that is still out is worth saying again, and
                    // the day count in the message changes with it.
                    DedupToken: DayBucket(now),
                    ClientId: renting.ClientId),
                cancellationToken);
        }
    }

    private async Task SweepUpcomingReservationsAsync(
        AgencySettingsSnapshot settings, DateTime now, CancellationToken cancellationToken)
    {
        var lead = Math.Max(settings.ReservationUpcomingLeadDays, 0);
        // Half-open on whole days, so "3 days ahead" includes all of the third day
        // and a sweep at 23:00 sees the same holds as one at 01:00.
        var horizon = now.Date.AddDays(lead + 1);

        var upcoming = await _context.Reservations
            .AsNoTracking()
            // Holds the agency has already committed to. A hold still awaiting
            // confirmation is the reservation screen's business (and the expiry
            // job's), not a pickup to prepare for.
            .Where(r => (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.Paid)
                        && r.StartDate != null
                        && r.StartDate >= now.Date
                        && r.StartDate < horizon)
            .Select(r => new
            {
                r.Id,
                r.StartDate,
                r.ClientId,
                ClientFirstName = r.Client != null ? r.Client.FirstName : null,
                ClientLastName = r.Client != null ? r.Client.LastName : null,
                Matricule = r.Car != null ? r.Car.Matricule : null,
                ModelName = r.Car != null && r.Car.Model != null ? r.Car.Model.Name : null,
            })
            .ToListAsync(cancellationToken);

        foreach (var reservation in upcoming)
        {
            var args = new NotificationArgs()
                .Set("car", CarLabel(reservation.ModelName, reservation.Matricule))
                .Set("client", PersonLabel(reservation.ClientFirstName, reservation.ClientLastName))
                .Set("days", WholeDaysBetween(now, reservation.StartDate!.Value))
                .SetDate("startDate", reservation.StartDate);

            await _notifications.NotifyStaffAsync(
                new StaffNotification(
                    NotificationKind.ReservationUpcoming,
                    NotificationMessages.ReservationUpcoming,
                    Permissions.ReservationRead,
                    NotificationSubject.Reservation,
                    reservation.Id,
                    $"/reservation/{reservation.Id}",
                    args,
                    // Once per hold — but keyed on the start date, so moving the
                    // booking earns a fresh heads-up for the new date.
                    DedupToken: IsoDate(reservation.StartDate),
                    ClientId: reservation.ClientId),
                cancellationToken);
        }
    }

    // -----------------------------------------------------------------------
    // Written to the client: before pickup, and before the return is due.
    // -----------------------------------------------------------------------

    private async Task SweepClientRemindersAsync(
        AgencySettingsSnapshot settings,
        DateTime now,
        HashSet<string> features,
        CancellationToken cancellationToken)
    {
        if (features.Contains(FeatureFlags.Rentings))
        {
            if (settings.ClientReminderDaysBeforeStart > 0)
            {
                await RemindClientsAboutRentingsAsync(
                    settings.ClientReminderDaysBeforeStart, now, starting: true, cancellationToken);
            }

            if (settings.ClientReminderDaysBeforeEnd > 0)
            {
                await RemindClientsAboutRentingsAsync(
                    settings.ClientReminderDaysBeforeEnd, now, starting: false, cancellationToken);
            }
        }

        if (features.Contains(FeatureFlags.Reservations) && settings.ClientReminderDaysBeforeStart > 0)
        {
            await RemindClientsAboutReservationsAsync(
                settings.ClientReminderDaysBeforeStart, now, cancellationToken);
        }
    }

    private async Task RemindClientsAboutRentingsAsync(
        int leadDays, DateTime now, bool starting, CancellationToken cancellationToken)
    {
        var horizon = now.Date.AddDays(leadDays + 1);

        var query = _context.Rentings.AsNoTracking();

        // Two reminders off one shape: the pickup is told to a hire that has not
        // started, the return to one that is running. Anything finished or
        // cancelled is neither.
        query = starting
            ? query.Where(r => r.RentingState == RentingState.NotYet
                               && r.StartDate != null
                               && r.StartDate >= now.Date && r.StartDate < horizon)
            : query.Where(r => r.RentingState == RentingState.InProgress
                               && r.EndDate != null
                               && r.EndDate >= now.Date && r.EndDate < horizon);

        var bookings = await query
            .Where(r => r.Client != null && r.Client.Email != null)
            .Select(r => new
            {
                r.Id,
                r.StartDate,
                r.EndDate,
                ClientId = r.ClientId!.Value,
                Email = r.Client!.Email,
                r.Client.MarketplaceUserId,
                Matricule = r.Car != null ? r.Car.Matricule : null,
                ModelName = r.Car != null && r.Car.Model != null ? r.Car.Model.Name : null,
            })
            .ToListAsync(cancellationToken);

        if (bookings.Count == 0)
        {
            return;
        }

        var languages = await LanguagesByUserAsync(
            bookings.Select(b => b.MarketplaceUserId), cancellationToken);

        foreach (var booking in bookings)
        {
            var on = starting ? booking.StartDate : booking.EndDate;

            var args = new NotificationArgs()
                .Set("car", CarLabel(booking.ModelName, booking.Matricule))
                .Set("days", WholeDaysBetween(now, on!.Value))
                .SetDate(starting ? "startDate" : "endDate", on);

            await _notifications.NotifyClientAsync(
                new ClientNotification(
                    starting ? NotificationKind.RentingStartingSoon : NotificationKind.RentingEndingSoon,
                    starting
                        ? NotificationMessages.ClientRentingStartingSoon
                        : NotificationMessages.ClientRentingEndingSoon,
                    booking.ClientId,
                    booking.Email,
                    LanguageOf(languages, booking.MarketplaceUserId),
                    NotificationSubject.Renting,
                    booking.Id,
                    args,
                    // Keyed on the date it is about, so one letter per booking per
                    // date — and a rescheduled booking is announced again.
                    DedupToken: IsoDate(on)),
                cancellationToken);
        }
    }

    private async Task RemindClientsAboutReservationsAsync(
        int leadDays, DateTime now, CancellationToken cancellationToken)
    {
        var horizon = now.Date.AddDays(leadDays + 1);

        var bookings = await _context.Reservations
            .AsNoTracking()
            .Where(r => (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.Paid)
                        && r.StartDate != null
                        && r.StartDate >= now.Date && r.StartDate < horizon
                        && r.Client != null && r.Client.Email != null)
            .Select(r => new
            {
                r.Id,
                r.StartDate,
                ClientId = r.ClientId!.Value,
                Email = r.Client!.Email,
                r.Client.MarketplaceUserId,
                Matricule = r.Car != null ? r.Car.Matricule : null,
                ModelName = r.Car != null && r.Car.Model != null ? r.Car.Model.Name : null,
            })
            .ToListAsync(cancellationToken);

        if (bookings.Count == 0)
        {
            return;
        }

        var languages = await LanguagesByUserAsync(
            bookings.Select(b => b.MarketplaceUserId), cancellationToken);

        foreach (var booking in bookings)
        {
            var args = new NotificationArgs()
                .Set("car", CarLabel(booking.ModelName, booking.Matricule))
                .Set("days", WholeDaysBetween(now, booking.StartDate!.Value))
                .SetDate("startDate", booking.StartDate);

            await _notifications.NotifyClientAsync(
                new ClientNotification(
                    NotificationKind.RentingStartingSoon,
                    NotificationMessages.ClientReservationStartingSoon,
                    booking.ClientId,
                    booking.Email,
                    LanguageOf(languages, booking.MarketplaceUserId),
                    NotificationSubject.Reservation,
                    booking.Id,
                    args,
                    DedupToken: IsoDate(booking.StartDate)),
                cancellationToken);
        }
    }

    // A client who signs in to the portal has chosen a language; write to them in
    // it. Read in one query for the batch rather than per letter.
    private async Task<Dictionary<string, string?>> LanguagesByUserAsync(
        IEnumerable<string?> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<string, string?>();
        }

        return await _context.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.PreferredLanguage, cancellationToken);
    }

    private static string? LanguageOf(Dictionary<string, string?> languages, string? userId) =>
        userId is not null && languages.TryGetValue(userId, out var language) ? language : null;

    // -----------------------------------------------------------------------

    // "Peugeot 208 — 123 TU 4567", and whichever half exists when one does not.
    private static string CarLabel(string? modelName, string? matricule)
    {
        var parts = new[] { modelName, matricule }
            .Where(part => !string.IsNullOrWhiteSpace(part));

        var label = string.Join(" — ", parts);

        return string.IsNullOrWhiteSpace(label) ? "—" : label;
    }

    private static string PersonLabel(string? firstName, string? lastName)
    {
        var label = $"{firstName} {lastName}".Trim();

        return string.IsNullOrWhiteSpace(label) ? "—" : label;
    }

    // Whole days, so the figure in the message matches the one the calculator
    // reports and does not shift with the hour the sweep happens to run at.
    private static int WholeDaysBetween(DateTime from, DateTime to) =>
        Math.Abs((int)Math.Round((to.Date - from.Date).TotalDays));

    private static string DayBucket(DateTime now) =>
        now.ToString(NotificationArgs.IsoDateFormat, CultureInfo.InvariantCulture);

    // The Monday of the current week, as a stable weekly bucket. Computed from
    // the date rather than from ISO week numbers, which turn over inconsistently
    // around the new year.
    private static string WeekBucket(DateTime now) =>
        now.Date.AddDays(-((int)now.Date.DayOfWeek + 6) % 7)
            .ToString(NotificationArgs.IsoDateFormat, CultureInfo.InvariantCulture);

    private static string IsoDate(DateTime? value) =>
        value?.ToString(NotificationArgs.IsoDateFormat, CultureInfo.InvariantCulture) ?? "-";
}
