using RemSolution.Application.Common.Features;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Dashboard.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Dashboard.Queries.GetBookingCalendarQuery
{
    // The agency's month at a glance: which cars go out on which day, which are
    // due back, and which holds are waiting to become hires.
    //
    // This is the landing screen's agenda, so every signed-in member of the agency
    // reaches it, and each HALF is gated on the module it reads rather than the
    // whole thing on one overview permission. A user who may see hires but not
    // holds gets the hires: half a calendar is less than a whole one, but it is
    // considerably more than the empty screen the old gate handed them. Nothing
    // here is actionable — the links the screen draws lead to the lists and
    // records, each of which enforces its own permission.
    public record GetBookingCalendarQuery(
        // Half-open [From, To). Defaults to the current calendar month; the screen
        // asks for its whole grid, adjacent-month days included, so the leading and
        // trailing cells are populated too.
        DateTime? From = null,
        DateTime? To = null,
        // Scopes the window to one branch, exactly as the home screen's figures are
        // scoped: a booking has no branch of its own and is placed at its car's, so
        // one whose car has been removed belongs to no branch and drops out.
        int? BranchId = null
    ) : IRequest<BookingCalendarDto>;

    public class GetBookingCalendarQueryHandler
        : IRequestHandler<GetBookingCalendarQuery, BookingCalendarDto>
    {
        // A calendar screen shows weeks, not quarters. Six weeks either side of a
        // month grid fits well inside this, and it stops a caller asking for a
        // decade of bookings through a screen that could not draw them.
        private const int MaxDays = 62;

        // Per source, so a busy month cannot turn a landing-page panel into a
        // thousand-row download. Each hire can contribute two entries (out and
        // back), so the answer holds at most three times this.
        private const int MaxRowsPerSource = 400;

        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;
        private readonly IIdentityService _identity;
        private readonly IUser _user;
        private readonly TimeProvider _dateTime;

        public GetBookingCalendarQueryHandler(
            IApplicationDbContext context,
            ITenantProvider tenant,
            IIdentityService identity,
            IUser user,
            TimeProvider dateTime)
        {
            _context = context;
            _tenant = tenant;
            _identity = identity;
            _user = user;
            _dateTime = dateTime;
        }

        public async Task<BookingCalendarDto> Handle(
            GetBookingCalendarQuery request, CancellationToken cancellationToken)
        {
            var (canRentings, canReservations) = await ModulesAsync(cancellationToken);

            var now = _dateTime.GetUtcNow().UtcDateTime;

            var from = request.From ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = request.To ?? from.AddMonths(1);

            // An inverted or empty window would otherwise return everything the
            // filters allow; it is clamped to a single day rather than refused, so
            // a screen sending a bad pair gets an empty day and not an error banner.
            if (to <= from)
                to = from.AddDays(1);

            if ((to - from).TotalDays > MaxDays)
                to = from.AddDays(MaxDays);

            // A hire is in the window when either of its two dates is, and each
            // date contributes its own entry below. Cancelled hires are left out
            // for the same reason the dashboard's period figures leave them out:
            // they are rows, but they are not work.
            var rentingsQuery = _context.Rentings.AsNoTracking();
            var reservationsQuery = _context.Reservations.AsNoTracking();

            if (request.BranchId is int branchId)
            {
                rentingsQuery = rentingsQuery.Where(r => r.Car != null && r.Car.BranchId == branchId);
                reservationsQuery = reservationsQuery.Where(r => r.Car != null && r.Car.BranchId == branchId);
            }

            var rentings = !canRentings ? new List<BookingRow>() : await rentingsQuery
                .Where(r => r.RentingState != RentingState.Cancelled
                            && ((r.StartDate >= from && r.StartDate < to)
                                || (r.EndDate >= from && r.EndDate < to)))
                .OrderBy(r => r.StartDate)
                .ThenBy(r => r.Id)
                .Select(r => new BookingRow(
                    r.Id,
                    r.StartDate,
                    r.EndDate,
                    r.RentingState,
                    r.CarId,
                    r.Car == null ? null : r.Car.Matricule,
                    r.Car == null || r.Car.Model == null ? null : r.Car.Model.Name,
                    r.ClientId,
                    r.Client == null ? null : r.Client.FirstName + " " + r.Client.LastName))
                .Take(MaxRowsPerSource + 1)
                .ToListAsync(cancellationToken);

            // Only holds that are still live: a converted one is now a hire and
            // already appears above as its pickup, and a rejected, expired or
            // cancelled one is not going to happen.
            var reservations = !canReservations ? new List<HoldRow>() : await reservationsQuery
                .Where(r => (r.Status == ReservationStatus.PendingConfirmation
                             || r.Status == ReservationStatus.Confirmed
                             || r.Status == ReservationStatus.Paid)
                            && r.StartDate >= from && r.StartDate < to)
                .OrderBy(r => r.StartDate)
                .ThenBy(r => r.Id)
                .Select(r => new HoldRow(
                    r.Id,
                    r.StartDate,
                    r.Status,
                    r.CarId,
                    r.Car == null ? null : r.Car.Matricule,
                    r.Car == null || r.Car.Model == null ? null : r.Car.Model.Name,
                    r.ClientId,
                    r.Client == null ? null : r.Client.FirstName + " " + r.Client.LastName))
                .Take(MaxRowsPerSource + 1)
                .ToListAsync(cancellationToken);

            // One row over the cap is how the tail is detected; it is not returned.
            var truncated = rentings.Count > MaxRowsPerSource || reservations.Count > MaxRowsPerSource;

            var events = new List<BookingCalendarEventDto>();

            foreach (var row in rentings.Take(MaxRowsPerSource))
            {
                if (row.StartDate >= from && row.StartDate < to)
                {
                    events.Add(new BookingCalendarEventDto
                    {
                        Kind = BookingCalendarEventKind.Pickup,
                        On = row.StartDate!.Value,
                        RentingId = row.Id,
                        CarId = row.CarId,
                        CarMatricule = row.CarMatricule,
                        CarModelName = row.CarModelName,
                        ClientId = row.ClientId,
                        ClientName = row.ClientName,
                        RentingState = row.RentingState,
                    });
                }

                if (row.EndDate >= from && row.EndDate < to)
                {
                    events.Add(new BookingCalendarEventDto
                    {
                        Kind = BookingCalendarEventKind.Return,
                        On = row.EndDate!.Value,
                        RentingId = row.Id,
                        CarId = row.CarId,
                        CarMatricule = row.CarMatricule,
                        CarModelName = row.CarModelName,
                        ClientId = row.ClientId,
                        ClientName = row.ClientName,
                        RentingState = row.RentingState,
                        // Still out, and it was due before now.
                        IsLate = row.RentingState == RentingState.InProgress && row.EndDate < now,
                    });
                }
            }

            foreach (var row in reservations.Take(MaxRowsPerSource))
            {
                events.Add(new BookingCalendarEventDto
                {
                    Kind = BookingCalendarEventKind.ReservationStart,
                    On = row.StartDate!.Value,
                    ReservationId = row.Id,
                    CarId = row.CarId,
                    CarMatricule = row.CarMatricule,
                    CarModelName = row.CarModelName,
                    ClientId = row.ClientId,
                    ClientName = row.ClientName,
                    ReservationStatus = row.Status,
                });
            }

            return new BookingCalendarDto
            {
                From = from,
                To = to,
                IsTruncated = truncated,
                // Chronological across the three kinds: the two sources are read
                // separately, and a day's entries are read top to bottom.
                Events = events
                    .OrderBy(e => e.On)
                    .ThenBy(e => e.Kind)
                    .ThenBy(e => e.CarMatricule)
                    .ToList(),
            };
        }

        // What this caller may see of the calendar. Same rule the navigation and
        // every list apply — feature on for the agency AND the read permission held
        // — asked imperatively because the two halves are gated separately and an
        // attribute cannot say "and also, only if" (see Entitlements).
        private async Task<(bool Rentings, bool Reservations)> ModulesAsync(
            CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            // No tenant (a platform admin outside a workspace): there is no agency
            // whose entitlements could apply, mirroring the feature behaviour.
            var features = _tenant.AgencyId is int agencyId
                ? await AgencyFeatureResolver.GetEnabledFeaturesAsync(
                    _context, agencyId, _dateTime.GetUtcNow(), cancellationToken)
                : FeatureFlags.All.ToHashSet();

            return (
                features.Contains(FeatureFlags.Rentings)
                    && await _identity.AuthorizeAsync(userId, Permissions.RentingRead),
                features.Contains(FeatureFlags.Reservations)
                    && await _identity.AuthorizeAsync(userId, Permissions.ReservationRead));
        }

        // The columns the entries are built from, projected straight out of SQL —
        // named types rather than anonymous ones so the loops above can take them.
        private sealed record BookingRow(
            int Id, DateTime? StartDate, DateTime? EndDate, RentingState RentingState,
            int? CarId, string? CarMatricule, string? CarModelName,
            int? ClientId, string? ClientName);

        private sealed record HoldRow(
            int Id, DateTime? StartDate, ReservationStatus Status,
            int? CarId, string? CarMatricule, string? CarModelName,
            int? ClientId, string? ClientName);
    }
}
