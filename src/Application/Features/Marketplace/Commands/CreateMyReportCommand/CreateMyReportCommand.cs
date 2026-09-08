using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Notifications;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using FluentValidation.Results;

namespace RemSolution.Application.Features.Marketplace.Commands.CreateMyReportCommand
{
    /// <summary>
    /// A customer complains about an agency to the platform. Raised against a
    /// booking of their own — a hold the agency confirmed, or a hire — because a
    /// complaint with no booking behind it is not arbitrable.
    /// <para>
    /// Deliberately NOT gated on any agency feature, for the same reason a review
    /// is not: an agency must not be able to switch off the complaints against it
    /// by dropping a module.
    /// </para>
    /// </summary>
    [Authorize(Policy = Policies.CustomerOnly)]
    public record CreateMyReportCommand : IRequest<int>
    {
        public int? ReservationId { get; init; }
        public int? RentingId { get; init; }
        public AgencyReportKind Kind { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    public class CreateMyReportCommandHandler : IRequestHandler<CreateMyReportCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly INotificationService _notifications;
        private readonly IUser _user;
        private readonly TimeProvider _dateTime;

        public CreateMyReportCommandHandler(
            IApplicationDbContext context,
            INotificationService notifications,
            IUser user,
            TimeProvider dateTime)
        {
            _context = context;
            _notifications = notifications;
            _user = user;
            _dateTime = dateTime;
        }

        public async Task<int> Handle(CreateMyReportCommand request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();
            var now = _dateTime.GetUtcNow().UtcDateTime;

            var booking = request.ReservationId is int reservationId
                ? await LoadReservationAsync(reservationId, userId, cancellationToken)
                : await LoadRentingAsync(request.RentingId!.Value, userId, cancellationToken);

            // Someone else's booking is indistinguishable from a missing one.
            Guard.Against.NotFound(request.ReservationId ?? request.RentingId ?? 0, booking);

            if (!booking.CanBeReported)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.ReservationId),
                        "You can only report a booking the agency had confirmed.")
                });
            }

            if (!Domain.Entities.AgencyReport.IsWithinReportingWindow(booking.ReferenceDate, now))
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.ReservationId),
                        $"This booking is too old to report — reports close " +
                        $"{Domain.Entities.AgencyReport.ReportingWindowDays} days after it ends.")
                });
            }

            // The kind is a label the arbitrator reads first, so it has to be
            // true: "the agency cancelled" is only available when it did.
            if (request.Kind == AgencyReportKind.CancelledBooking && !booking.CancelledByAgency)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Kind),
                        "This booking was not cancelled by the agency.")
                });
            }

            // The unique index per booking is the real guarantee; this turns the
            // race that loses into a readable message instead of a 500.
            var alreadyReported = await _context.AgencyReports.AnyAsync(
                r => (request.ReservationId != null && r.ReservationId == request.ReservationId)
                     || (request.RentingId != null && r.RentingId == request.RentingId),
                cancellationToken);

            if (alreadyReported)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.ReservationId),
                        "You have already reported this booking.")
                });
            }

            var report = Domain.Entities.AgencyReport.Create(
                booking.AgencyId,
                request.Kind,
                request.Message,
                now,
                reservationId: request.ReservationId,
                rentingId: request.RentingId,
                clientId: booking.ClientId,
                reporterUserId: userId,
                reporterName: booking.ReporterName,
                bookingSummary: booking.Summary,
                agencyCancellationReason: booking.CancellationReason);

            _context.AgencyReports.Add(report);
            await _context.SaveChangesAsync(cancellationToken);

            // After the commit: the agency being told is not worth undoing the
            // report over. The customer has no agency claim, so the tenant the
            // alert belongs to has to be pushed explicitly.
            using var _ = AmbientTenant.Push(booking.AgencyId);

            await _notifications.NotifyStaffAsync(
                new StaffNotification(
                    NotificationKind.AgencyReport,
                    NotificationMessages.AgencyReportOpened,
                    Permission: null,
                    NotificationSubject.AgencyReport,
                    report.Id,
                    "/my-agency/reports",
                    new NotificationArgs()
                        .Set("reference", $"#{report.Id}")
                        .Set("client", booking.ReporterName ?? "—"),
                    DedupToken: "opened",
                    ClientId: booking.ClientId),
                cancellationToken);

            return report.Id;
        }

        // What the report needs from the booking, whichever kind it is. Read
        // cross-tenant (the customer has no tenant claim) and proven theirs by
        // the same link the other customer commands use.
        private sealed record ReportableBooking(
            int AgencyId,
            int? ClientId,
            string? ReporterName,
            string? Summary,
            string? CancellationReason,
            bool CanBeReported,
            bool CancelledByAgency,
            DateTime? ReferenceDate);

        private async Task<ReportableBooking?> LoadReservationAsync(
            int id, string userId, CancellationToken cancellationToken)
        {
            var row = await _context.Reservations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.Id == id && r.Client != null && r.Client.MarketplaceUserId == userId)
                .Select(r => new
                {
                    r.AgencyId,
                    r.ClientId,
                    ClientFirstName = r.Client!.FirstName,
                    ClientLastName = r.Client!.LastName,
                    CarBrandName = r.Car != null && r.Car.Model != null && r.Car.Model.Brand != null
                        ? r.Car.Model.Brand.Name
                        : null,
                    CarModelName = r.Car != null && r.Car.Model != null ? r.Car.Model.Name : null,
                    r.StartDate,
                    r.EndDate,
                    r.Status,
                    r.CancelledAt,
                    r.CancelledByCustomer,
                    r.CancelledAfterConfirmation,
                    r.CancelledReason,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (row is null)
            {
                return null;
            }

            var cancelledByAgency = row.Status == ReservationStatus.Cancelled
                                    && row.CancelledAfterConfirmation
                                    && !row.CancelledByCustomer;

            return new ReportableBooking(
                row.AgencyId,
                row.ClientId,
                ReservationAlerts.PersonLabel(row.ClientFirstName, row.ClientLastName),
                Summary(row.CarBrandName, row.CarModelName, row.StartDate, row.EndDate),
                row.CancelledReason,
                CanBeReported: Domain.Entities.AgencyReport.CanReport(
                    row.Status, row.CancelledAfterConfirmation),
                cancelledByAgency,
                // A cancelled hold is timed from the cancellation; a live one from
                // the day it was due to end.
                ReferenceDate: row.CancelledAt ?? row.EndDate);
        }

        private async Task<ReportableBooking?> LoadRentingAsync(
            int id, string userId, CancellationToken cancellationToken)
        {
            var row = await _context.Rentings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.Id == id && r.Client != null && r.Client.MarketplaceUserId == userId)
                .Select(r => new
                {
                    r.AgencyId,
                    r.ClientId,
                    ClientFirstName = r.Client!.FirstName,
                    ClientLastName = r.Client!.LastName,
                    CarBrandName = r.Car != null && r.Car.Model != null && r.Car.Model.Brand != null
                        ? r.Car.Model.Brand.Name
                        : null,
                    CarModelName = r.Car != null && r.Car.Model != null ? r.Car.Model.Name : null,
                    r.StartDate,
                    r.EndDate,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (row is null)
            {
                return null;
            }

            return new ReportableBooking(
                row.AgencyId,
                row.ClientId,
                ReservationAlerts.PersonLabel(row.ClientFirstName, row.ClientLastName),
                Summary(row.CarBrandName, row.CarModelName, row.StartDate, row.EndDate),
                CancellationReason: null,
                // A hire happened: there is always something to answer for.
                CanBeReported: true,
                CancelledByAgency: false,
                ReferenceDate: row.EndDate);
        }

        // "Renault Clio · 2026-09-12 → 2026-09-15". Snapshotted onto the report
        // because the platform administrator who reads it has no tenant claim and
        // so cannot join back to the car (see AgencyReport).
        private static string Summary(
            string? brand, string? model, DateTime? start, DateTime? end)
        {
            var car = string.Join(' ', new[] { brand, model }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

            var dates = $"{ReservationAlerts.IsoDate(start)} → {ReservationAlerts.IsoDate(end)}";

            return car.Length == 0 ? dates : $"{car} · {dates}";
        }
    }
}

namespace RemSolution.Application.Features.Marketplace.Commands.CreateMyReportCommand
{
    public class CreateMyReportCommandValidator : AbstractValidator<CreateMyReportCommand>
    {
        public CreateMyReportCommandValidator()
        {
            // Exactly one anchor. The entity enforces it too, and so does a check
            // constraint — this is the one that produces a 400 rather than a 500.
            RuleFor(v => v)
                .Must(v => (v.ReservationId is null) != (v.RentingId is null))
                .WithMessage("A report is about exactly one booking.")
                .WithName(nameof(CreateMyReportCommand.ReservationId));

            RuleFor(v => v.ReservationId).GreaterThan(0).When(v => v.ReservationId is not null);
            RuleFor(v => v.RentingId).GreaterThan(0).When(v => v.RentingId is not null);
            RuleFor(v => v.Kind).IsInEnum();
            RuleFor(v => v.Message)
                .NotEmpty()
                .MaximumLength(Domain.Entities.AgencyReport.MaxMessageLength);
        }
    }
}
