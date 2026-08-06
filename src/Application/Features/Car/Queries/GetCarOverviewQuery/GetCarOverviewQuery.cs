using RemSolution.Application.Common.Features;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Car.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Car.Queries.GetCarOverviewQuery
{
    /// <summary>
    /// Everything one car's page shows beyond the car's own facts: how hard it has
    /// been working, what it billed, how it was rated, who has it booked, and what
    /// it has cost lately.
    /// <para>
    /// One call rather than four. The tiles and the lists under them are read
    /// together and compared against each other — a utilization figure taken at one
    /// moment beside a booking list taken at another is how a page ends up claiming
    /// a car is free while showing the hire it is out on.
    /// </para>
    /// <para>
    /// The request-level gate is the car's own (<c>Car.Read</c> + the Cars
    /// feature), because this reads a car. Everything it borrows from another
    /// module is gated a second time, imperatively, on THAT module — see
    /// <see cref="Entitlements"/> for why the attributes cannot say "and also,
    /// only if". A section the caller may not see comes back null rather than
    /// zeroed, so the page draws no card that would lead somewhere forbidden.
    /// </para>
    /// </summary>
    [Authorize(Policy = Permissions.CarRead)]
    [RequiresFeature(FeatureFlags.Cars)]
    public record GetCarOverviewQuery(int Id) : IRequest<CarOverviewDto>;

    public class GetCarOverviewQueryHandler : IRequestHandler<GetCarOverviewQuery, CarOverviewDto>
    {
        /// <summary>
        /// The window the usage tiles cover. A quarter: long enough that a fortnight
        /// off the road does not read as a dead car, short enough to still describe
        /// the vehicle as it is rather than as it was two summers ago.
        /// </summary>
        private const int WindowDays = 90;

        /// <summary>
        /// The compact lists are a peek, not the list. Both sections carry a link to
        /// the screen that owns the rest.
        /// </summary>
        private const int BookingRows = 5;
        private const int ExpenseRows = 5;

        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;
        private readonly IAgencySettingsProvider _settings;
        private readonly IIdentityService _identity;
        private readonly IUser _user;
        private readonly TimeProvider _dateTime;

        public GetCarOverviewQueryHandler(
            IApplicationDbContext context,
            ITenantProvider tenant,
            IAgencySettingsProvider settings,
            IIdentityService identity,
            IUser user,
            TimeProvider dateTime)
        {
            _context = context;
            _tenant = tenant;
            _settings = settings;
            _identity = identity;
            _user = user;
            _dateTime = dateTime;
        }

        public async Task<CarOverviewDto> Handle(
            GetCarOverviewQuery request, CancellationToken cancellationToken)
        {
            var agencyId = _tenant.AgencyId ?? throw new UnauthorizedAccessException();
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            var carId = request.Id;

            // The car is read through the tenant-filtered set, so another agency's
            // plate is a 404 here exactly as it is on the car itself — this endpoint
            // must not become the one that confirms a foreign car exists.
            var exists = await _context.Cars
                .AnyAsync(c => c.Id == carId, cancellationToken);

            if (!exists)
                throw new NotFoundException(nameof(Domain.Entities.Car), carId.ToString());

            var currency = (await _settings.GetAsync(agencyId, cancellationToken)).CurrencyCode;
            var now = _dateTime.GetUtcNow().UtcDateTime;

            // Half-open [from, to) over whole UTC days, ending with today included:
            // the app's one window convention, and the same wall-clock-stamped-UTC
            // reading every date in the API gets.
            var to = DateTime.SpecifyKind(now.Date.AddDays(1), DateTimeKind.Utc);
            var from = to.AddDays(-WindowDays);

            var features = await AgencyFeatureResolver.GetEnabledFeaturesAsync(
                _context, agencyId, _dateTime.GetUtcNow(), cancellationToken);

            // The same rule the navigation applies: the module on for the agency AND
            // the read permission held. An agency administrator passes every
            // permission policy by role, so this reduces to the feature for them.
            async Task<bool> CanAsync(string feature, string permission) =>
                features.Contains(feature) && await _identity.AuthorizeAsync(userId, permission);

            var canRentings = await CanAsync(FeatureFlags.Rentings, Permissions.RentingRead);
            var canExpenses = await CanAsync(FeatureFlags.Expenses, Permissions.ExpenseRead);

            return new CarOverviewDto
            {
                CarId = carId,
                Currency = currency,
                From = from,
                To = to,
                Usage = canRentings
                    ? await UsageAsync(carId, from, to, now, currency, cancellationToken)
                    : null,
                Rating = await RatingAsync(carId, agencyId, cancellationToken),
                Bookings = canRentings
                    ? await BookingsAsync(carId, now, cancellationToken)
                    : null,
                BookingsTotal = canRentings
                    ? await _context.Rentings.CountAsync(
                        r => r.CarId == carId && r.RentingState != RentingState.Cancelled,
                        cancellationToken)
                    : null,
                Expenses = canExpenses ? await ExpensesAsync(carId, cancellationToken) : null,
                ExpensesTotal = canExpenses
                    ? new MoneyDto(await ExpensesTotalAsync(carId, from, to, cancellationToken), currency)
                    : null,
            };
        }

        // --- Usage -------------------------------------------------------------

        /// <summary>
        /// How busy the car was over the window, and what that billed.
        /// </summary>
        /// <remarks>
        /// Two different questions, so two different reads of the same hires:
        /// <list type="bullet">
        /// <item>Occupancy counts DAYS THE CAR WAS OUT, so a hire that started
        /// before the window still occupies its tail of it, and two bookings
        /// touching the same day are one day out. That is what keeps the percentage
        /// a percentage.</item>
        /// <item>Money follows the statistics report's attribution — a hire belongs
        /// whole to the period it STARTS in — because an agency reconciles the two
        /// screens against each other, and a revenue figure that disagreed with the
        /// report would be the wrong one whichever was right.</item>
        /// </list>
        /// </remarks>
        private async Task<CarUsageDto> UsageAsync(
            int carId, DateTime from, DateTime to, DateTime now, string currency,
            CancellationToken cancellationToken)
        {
            // Anything that could touch the window: started before it ends, and
            // either has no end or ends after it starts. A car's hires over a
            // quarter are counted in tens, so they are folded here rather than in
            // SQL — the day-by-day occupancy below cannot be expressed as a sum.
            var hires = await _context.Rentings
                .Where(r => r.CarId == carId
                            && r.RentingState != RentingState.Cancelled
                            && r.StartDate != null
                            && r.StartDate < to
                            && (r.EndDate == null || r.EndDate > from))
                .Select(r => new { Start = r.StartDate!.Value, r.EndDate })
                .ToListAsync(cancellationToken);

            // One flag per day of the window. Overlapping bookings — which the
            // availability rule should prevent but historic data can still hold —
            // collapse to the one day they share instead of counting twice.
            var occupied = new bool[WindowDays];

            foreach (var hire in hires)
            {
                // A hire still running has no return date yet; it holds the car up
                // to now, not to the end of the window (which is the end of today).
                var start = hire.Start < from ? from : hire.Start;
                var end = hire.EndDate ?? now;
                if (end > to) end = to;

                for (var day = start.Date; day < end; day = day.AddDays(1))
                {
                    var index = (int)(day - from).TotalDays;
                    if (index >= 0 && index < WindowDays) occupied[index] = true;
                }
            }

            var rentedDays = occupied.Count(day => day);

            // The money half, on the report's rule: hires that STARTED in the
            // window, a cancelled one billing the fee the agency kept rather than
            // the price it would have earned (see ClientCreditRows).
            var started = await _context.Rentings
                .Where(r => r.CarId == carId
                            && r.StartDate != null
                            && r.StartDate >= from && r.StartDate < to)
                .Select(r => new
                {
                    r.RentingState,
                    Price = r.Price == null ? 0m : r.Price.Amount,
                    Fee = r.CancellationFee == null ? 0m : r.CancellationFee.Amount
                })
                .ToListAsync(cancellationToken);

            var charged = started.Sum(
                h => h.RentingState == RentingState.Cancelled ? h.Fee : h.Price);

            return new CarUsageDto
            {
                RentedDays = rentedDays,
                WindowDays = WindowDays,
                UtilizationPercent = (int)Math.Round(rentedDays * 100.0 / WindowDays),
                Charged = new MoneyDto(charged, currency),
                Rentings = started.Count(h => h.RentingState != RentingState.Cancelled),
            };
        }

        // --- Rating ------------------------------------------------------------

        /// <summary>
        /// What customers scored this car. A review hangs off the RENTING it was
        /// left on (there is no free-floating review), so the car's reviews are the
        /// ones whose renting is one of its own.
        /// </summary>
        /// <remarks>
        /// <c>AgencyReview</c> is deliberately platform-level and carries no global
        /// tenant filter, so the agency is named here explicitly; the renting side
        /// of the test goes through the tenant-filtered set, which is the belt to
        /// that braces.
        /// </remarks>
        private async Task<CarRatingDto> RatingAsync(
            int carId, int agencyId, CancellationToken cancellationToken)
        {
            var ratings = await _context.AgencyReviews
                .Where(review => review.AgencyId == agencyId
                                 && _context.Rentings.Any(
                                     r => r.Id == review.RentingId && r.CarId == carId))
                .Select(review => review.Rating)
                .ToListAsync(cancellationToken);

            return new CarRatingDto
            {
                Count = ratings.Count,
                // One decimal: the difference between 4.46 and 4.5 is not something
                // eleven reviews can support, and the tile shows one digit anyway.
                Average = ratings.Count == 0 ? null : Math.Round(ratings.Average(), 1),
            };
        }

        // --- Bookings ----------------------------------------------------------

        /// <summary>
        /// The hire running now and the ones booked after it, soonest first — what
        /// the desk is asked when the phone rings about this plate. Finished and
        /// cancelled hires are history and live in the page's table below.
        /// </summary>
        private async Task<IList<CarBookingDto>> BookingsAsync(
            int carId, DateTime now, CancellationToken cancellationToken) =>
            await _context.Rentings
                .Where(r => r.CarId == carId
                            && (r.RentingState == RentingState.InProgress
                                || r.RentingState == RentingState.NotYet))
                // Running first, then the queue behind it in the order it will be
                // worked through.
                .OrderByDescending(r => r.RentingState == RentingState.InProgress)
                .ThenBy(r => r.StartDate)
                .ThenBy(r => r.Id)
                .Take(BookingRows)
                .Select(r => new CarBookingDto
                {
                    RentingId = r.Id,
                    ClientId = r.ClientId,
                    ClientName = r.Client == null
                        ? null
                        : r.Client.FirstName + " " + r.Client.LastName,
                    ClientEmail = r.Client == null ? null : r.Client.Email,
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                    State = r.RentingState,
                    Price = r.Price == null ? null : new MoneyDto(r.Price.Amount, r.Price.Currency),
                    IsLate = r.RentingState == RentingState.InProgress
                             && r.EndDate != null && r.EndDate < now,
                })
                .ToListAsync(cancellationToken);

        // --- Expenses ----------------------------------------------------------

        /// <summary>The latest costs booked against the car, newest first.</summary>
        private async Task<IList<CarExpenseDto>> ExpensesAsync(
            int carId, CancellationToken cancellationToken) =>
            await _context.Expenses
                .Where(e => e.CarId == carId)
                .OrderByDescending(e => e.ExpenseDate)
                .ThenByDescending(e => e.Id)
                .Take(ExpenseRows)
                .Select(e => new CarExpenseDto
                {
                    Id = e.Id,
                    TypeName = e.ExpenseType == null ? null : e.ExpenseType.Name,
                    Description = e.Description,
                    ExpenseDate = e.ExpenseDate,
                    Amount = e.ExpenseAmount == null
                        ? null
                        : new MoneyDto(e.ExpenseAmount.Amount, e.ExpenseAmount.Currency),
                    IsUnpaid = e.ExpenseAmount != null
                               && e.ExpenseAmount.Amount
                                  > (e.PaidAmount == null ? 0m : e.PaidAmount.Amount),
                })
                .ToListAsync(cancellationToken);

        /// <summary>
        /// What the car cost over the same window the usage tiles cover, so the two
        /// figures can be read against each other. Dated by when the money moved,
        /// which is the only date an expense has.
        /// </summary>
        private async Task<decimal> ExpensesTotalAsync(
            int carId, DateTime from, DateTime to, CancellationToken cancellationToken) =>
            await _context.Expenses
                .Where(e => e.CarId == carId
                            && e.ExpenseAmount != null
                            && e.ExpenseDate >= from && e.ExpenseDate < to)
                .SumAsync(e => e.ExpenseAmount!.Amount, cancellationToken);
    }
}
