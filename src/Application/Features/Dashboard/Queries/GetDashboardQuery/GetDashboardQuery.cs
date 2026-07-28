using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Dashboard.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Dashboard.Queries.GetDashboardQuery
{
    // The agency's overview screen. Every figure comes from the tenant-filtered
    // sets, so an agency only ever sees its own numbers. Read-only and counted
    // live rather than denormalised — an agency's data is small enough that the
    // aggregates are cheap, and a stale KPI is worse than a slightly slower one.
    [Authorize(Policy = Permissions.DashboardView)]
    [RequiresFeature(FeatureFlags.Dashboard)]
    public record GetDashboardQuery(
        // Window for the period-scoped figures; defaults to the current calendar
        // month when either bound is omitted. Any [from, to) is accepted, so the
        // screen's presets and a hand-picked pair of dates take the same path.
        DateTime? From = null,
        DateTime? To = null,
        // Buckets of history in Series, ending with the window's LAST bucket (so
        // a three-month window's chart ends on its third month, not its first).
        int Periods = 6,
        // How finely the series is sliced. Independent of the window: "this
        // quarter by day" and "five years by year" are both askable.
        DashboardGranularity Granularity = DashboardGranularity.Month
    ) : IRequest<DashboardDto>;

    public class GetDashboardQueryHandler : IRequestHandler<GetDashboardQuery, DashboardDto>
    {
        // Per-granularity ceilings on the number of buckets. They bound the chart
        // as much as the query: ninety points is already more than a line chart
        // can show legibly, and a decade of years is more history than the app has.
        private const int MaxDayBuckets = 90;
        private const int MaxMonthBuckets = 24;
        private const int MaxYearBuckets = 10;

        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;
        private readonly IAgencySettingsProvider _settings;
        private readonly TimeProvider _dateTime;

        public GetDashboardQueryHandler(
            IApplicationDbContext context, ITenantProvider tenant,
            IAgencySettingsProvider settings, TimeProvider dateTime)
        {
            _context = context;
            _tenant = tenant;
            _settings = settings;
            _dateTime = dateTime;
        }

        public async Task<DashboardDto> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
        {
            var agencyId = _tenant.AgencyId ?? throw new UnauthorizedAccessException();
            var currency = (await _settings.GetAsync(agencyId, cancellationToken)).CurrencyCode;

            var now = _dateTime.GetUtcNow().UtcDateTime;

            // Half-open window [start, end) — the same convention the booking
            // overlap rule uses, so a payment at midnight lands in one month only.
            var periodStart = request.From ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var periodEnd = request.To ?? periodStart.AddMonths(1);

            // --- Clients ---
            // Audit stamps are DateTimeOffset (unlike domain DateTimes, which are
            // UTC-enforced at the persistence boundary), so the window bounds are
            // lifted to UTC offsets to compare against CreatedOn.
            var periodStartOffset = new DateTimeOffset(periodStart, TimeSpan.Zero);
            var periodEndOffset = new DateTimeOffset(periodEnd, TimeSpan.Zero);

            var totalClients = await _context.Clients.CountAsync(cancellationToken);
            var newClientsInPeriod = await _context.Clients
                .CountAsync(c => c.CreatedOn >= periodStartOffset && c.CreatedOn < periodEndOffset,
                            cancellationToken);
            var flaggedClients = await _context.Clients.CountAsync(c => c.IsFlagged, cancellationToken);

            // --- Fleet ---
            var totalCars = await _context.Cars.CountAsync(cancellationToken);
            var activeCars = await _context.Cars
                .CountAsync(c => c.Status == CarStatus.Active, cancellationToken);
            var newCarsInPeriod = await _context.Cars
                .CountAsync(c => c.CreatedOn >= periodStartOffset && c.CreatedOn < periodEndOffset,
                            cancellationToken);
            var carsOnRent = await _context.Rentings
                .Where(r => r.RentingState == RentingState.InProgress && r.CarId != null)
                .Select(r => r.CarId)
                .Distinct()
                .CountAsync(cancellationToken);

            // --- Bookings ---
            var rentingsInProgress = await _context.Rentings
                .CountAsync(r => r.RentingState == RentingState.InProgress, cancellationToken);
            var rentingsUpcoming = await _context.Rentings
                .CountAsync(r => r.RentingState == RentingState.NotYet, cancellationToken);
            var pendingRequests = await _context.Reservations
                .CountAsync(r => r.Status == ReservationStatus.PendingConfirmation, cancellationToken);
            var returnsDueInPeriod = await _context.Rentings
                .CountAsync(r => r.RentingState == RentingState.InProgress
                                 && r.EndDate >= periodStart && r.EndDate < periodEnd, cancellationToken);
            var rentingsStartedInPeriod = await _context.Rentings
                .CountAsync(r => r.RentingState != RentingState.Cancelled
                                 && r.StartDate >= periodStart && r.StartDate < periodEnd, cancellationToken);

            // --- Money in the period ---
            var chargedInPeriod = await _context.Rentings
                .Where(r => r.RentingState != RentingState.Cancelled
                            && r.Price != null
                            && r.StartDate >= periodStart && r.StartDate < periodEnd)
                .SumAsync(r => r.Price!.Amount, cancellationToken);

            var collectedInPeriod = await _context.Payments
                .Where(p => p.PayementAmount != null
                            && p.PayementDate >= periodStart && p.PayementDate < periodEnd)
                .SumAsync(p => p.PayementAmount!.Amount, cancellationToken);

            var expensesInPeriod = await _context.Expenses
                .Where(e => e.ExpenseAmount != null
                            && e.ExpenseDate >= periodStart && e.ExpenseDate < periodEnd)
                .SumAsync(e => e.ExpenseAmount!.Amount, cancellationToken);

            // --- All-time outstanding, both directions ---
            var debtors = _context.Clients
                .Select(c => new
                {
                    Charged =
                        c.Rentings!
                            .Where(r => r.RentingState != RentingState.Cancelled && r.Price != null)
                            .Sum(r => r.Price!.Amount)
                        + c.Reservations!
                            .Where(r => (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.Paid)
                                        && r.Price != null)
                            .Sum(r => r.Price!.Amount),
                    Paid = c.Payments!
                        .Where(p => p.PayementAmount != null)
                        .Sum(p => p.PayementAmount!.Amount),
                })
                .Where(x => x.Charged - x.Paid > 0m);

            var clientsOutstanding = await debtors.SumAsync(x => x.Charged - x.Paid, cancellationToken);
            var clientsInDebtCount = await debtors.CountAsync(cancellationToken);

            var expensesOutstanding = await _context.Expenses
                .Where(e => e.ExpenseAmount != null
                            && e.ExpenseAmount.Amount > (e.PaidAmount == null ? 0m : e.PaidAmount.Amount))
                .SumAsync(e => e.ExpenseAmount!.Amount - (e.PaidAmount == null ? 0m : e.PaidAmount.Amount),
                          cancellationToken);

            var series = await BuildSeriesAsync(
                periodStart, periodEnd, request.Periods, request.Granularity, currency, cancellationToken);

            return new DashboardDto
            {
                Currency = currency,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                TotalClients = totalClients,
                NewClientsInPeriod = newClientsInPeriod,
                FlaggedClients = flaggedClients,
                ClientsInDebtCount = clientsInDebtCount,
                TotalCars = totalCars,
                ActiveCars = activeCars,
                CarsOnRent = carsOnRent,
                NewCarsInPeriod = newCarsInPeriod,
                RentingsInProgress = rentingsInProgress,
                RentingsUpcoming = rentingsUpcoming,
                PendingReservationRequests = pendingRequests,
                ReturnsDueInPeriod = returnsDueInPeriod,
                RentingsStartedInPeriod = rentingsStartedInPeriod,
                ChargedInPeriod = new MoneyDto(chargedInPeriod, currency),
                CollectedInPeriod = new MoneyDto(collectedInPeriod, currency),
                ExpensesInPeriod = new MoneyDto(expensesInPeriod, currency),
                NetInPeriod = new MoneyDto(collectedInPeriod - expensesInPeriod, currency),
                ClientsOutstanding = new MoneyDto(clientsOutstanding, currency),
                ExpensesOutstanding = new MoneyDto(expensesOutstanding, currency),
                Granularity = request.Granularity,
                Series = series,
            };
        }

        // Fleet, client and money activity per bucket over the trailing window.
        // Empty buckets are emitted as zeroes so the caller gets a contiguous
        // series and never has to fill gaps itself.
        //
        // Everything is grouped by calendar day in SQL and folded into buckets
        // here. One shape of query serves all three granularities, and the day
        // rows cost nothing extra: only days with activity come back, so a
        // five-year window returns as many rows as the agency had busy days, not
        // 1826 of them.
        private async Task<IList<DashboardPeriodPointDto>> BuildSeriesAsync(
            DateTime periodStart, DateTime periodEnd, int periodsRequested,
            DashboardGranularity granularity, string currency, CancellationToken cancellationToken)
        {
            var buckets = Math.Clamp(periodsRequested, 1, granularity switch
            {
                DashboardGranularity.Day => MaxDayBuckets,
                DashboardGranularity.Year => MaxYearBuckets,
                _ => MaxMonthBuckets,
            });

            // The series ends on the window's last bucket, not its first. The
            // window is half-open, so that is the bucket containing the tick
            // before periodEnd — for a single-month window the two are the same
            // bucket, but for "last 3 months" or "this year" anchoring on
            // periodStart would end the chart before the figures above it.
            var last = periodEnd > periodStart ? periodEnd.AddTicks(-1) : periodStart;

            var seriesEnd = Advance(Truncate(last, granularity), granularity, 1);
            var seriesStart = Advance(seriesEnd, granularity, -buckets);

            // Audit stamps are DateTimeOffset; the domain dates are UTC DateTime.
            var seriesStartOffset = new DateTimeOffset(seriesStart, TimeSpan.Zero);
            var seriesEndOffset = new DateTimeOffset(seriesEnd, TimeSpan.Zero);

            var collected = await _context.Payments
                .Where(p => p.PayementAmount != null
                            && p.PayementDate >= seriesStart && p.PayementDate < seriesEnd)
                .GroupBy(p => new { p.PayementDate!.Value.Year, p.PayementDate!.Value.Month, p.PayementDate!.Value.Day })
                .Select(g => new DailyMoney(g.Key.Year, g.Key.Month, g.Key.Day, g.Sum(p => p.PayementAmount!.Amount)))
                .ToListAsync(cancellationToken);

            var spent = await _context.Expenses
                .Where(e => e.ExpenseAmount != null
                            && e.ExpenseDate >= seriesStart && e.ExpenseDate < seriesEnd)
                .GroupBy(e => new { e.ExpenseDate.Year, e.ExpenseDate.Month, e.ExpenseDate.Day })
                .Select(g => new DailyMoney(g.Key.Year, g.Key.Month, g.Key.Day, g.Sum(e => e.ExpenseAmount!.Amount)))
                .ToListAsync(cancellationToken);

            // Cars and clients are dated by when they were recorded (the audit
            // stamp), which is the only "added on" the model has.
            var newCars = await _context.Cars
                .Where(c => c.CreatedOn >= seriesStartOffset && c.CreatedOn < seriesEndOffset)
                .GroupBy(c => new { c.CreatedOn!.Value.Year, c.CreatedOn!.Value.Month, c.CreatedOn!.Value.Day })
                .Select(g => new DailyCount(g.Key.Year, g.Key.Month, g.Key.Day, g.Count()))
                .ToListAsync(cancellationToken);

            var newClients = await _context.Clients
                .Where(c => c.CreatedOn >= seriesStartOffset && c.CreatedOn < seriesEndOffset)
                .GroupBy(c => new { c.CreatedOn!.Value.Year, c.CreatedOn!.Value.Month, c.CreatedOn!.Value.Day })
                .Select(g => new DailyCount(g.Key.Year, g.Key.Month, g.Key.Day, g.Count()))
                .ToListAsync(cancellationToken);

            // Dated by when the hire starts, matching ChargedInPeriod above so the
            // chart and the money tile tell the same story.
            var rentings = await _context.Rentings
                .Where(r => r.RentingState != RentingState.Cancelled
                            && r.StartDate >= seriesStart && r.StartDate < seriesEnd)
                .GroupBy(r => new { r.StartDate!.Value.Year, r.StartDate!.Value.Month, r.StartDate!.Value.Day })
                .Select(g => new DailyCount(g.Key.Year, g.Key.Month, g.Key.Day, g.Count()))
                .ToListAsync(cancellationToken);

            var points = new List<DashboardPeriodPointDto>(buckets);

            for (var i = 0; i < buckets; i++)
            {
                var bucketStart = Advance(seriesStart, granularity, i);
                var bucketEnd = Advance(seriesStart, granularity, i + 1);

                points.Add(new DashboardPeriodPointDto
                {
                    BucketStart = bucketStart,
                    BucketEnd = bucketEnd,
                    NewCars = SumCounts(newCars, bucketStart, bucketEnd),
                    NewClients = SumCounts(newClients, bucketStart, bucketEnd),
                    RentingsStarted = SumCounts(rentings, bucketStart, bucketEnd),
                    Collected = new MoneyDto(SumMoney(collected, bucketStart, bucketEnd), currency),
                    Expenses = new MoneyDto(SumMoney(spent, bucketStart, bucketEnd), currency),
                });
            }

            return points;
        }

        // Day-grouped rows, projected straight out of SQL. Named types rather than
        // anonymous ones so the folding helpers below can take them.
        private sealed record DailyMoney(int Year, int Month, int Day, decimal Total)
        {
            public DateTime On => new(Year, Month, Day, 0, 0, 0, DateTimeKind.Utc);
        }

        private sealed record DailyCount(int Year, int Month, int Day, int Count)
        {
            public DateTime On => new(Year, Month, Day, 0, 0, 0, DateTimeKind.Utc);
        }

        private static decimal SumMoney(IEnumerable<DailyMoney> rows, DateTime start, DateTime end) =>
            rows.Where(r => r.On >= start && r.On < end).Sum(r => r.Total);

        private static int SumCounts(IEnumerable<DailyCount> rows, DateTime start, DateTime end) =>
            rows.Where(r => r.On >= start && r.On < end).Sum(r => r.Count);

        // The start of the calendar bucket a moment falls in.
        private static DateTime Truncate(DateTime value, DashboardGranularity granularity) =>
            granularity switch
            {
                DashboardGranularity.Day => new DateTime(value.Year, value.Month, value.Day, 0, 0, 0, DateTimeKind.Utc),
                DashboardGranularity.Year => new DateTime(value.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                _ => new DateTime(value.Year, value.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            };

        // Calendar arithmetic, so a month step stays a month across February and a
        // year step lands on the same date next year.
        private static DateTime Advance(DateTime value, DashboardGranularity granularity, int steps) =>
            granularity switch
            {
                DashboardGranularity.Day => value.AddDays(steps),
                DashboardGranularity.Year => value.AddYears(steps),
                _ => value.AddMonths(steps),
            };
    }
}
