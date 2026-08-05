using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Statistics.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Statistics.Queries.GetStatisticsQuery
{
    /// <summary>
    /// The agency's rental and money figures, month by month or year by year, for
    /// the whole fleet or for one car.
    /// <para>
    /// Gated exactly like the dashboard, and for the same reason: this is a
    /// read-only overview crossing the renting, payment and expense modules, so one
    /// "may see the overview screens" permission decides it rather than three
    /// per-module checks that would leave a user with a table of half-figures.
    /// </para>
    /// </summary>
    [Authorize(Policy = Permissions.DashboardView)]
    [RequiresFeature(FeatureFlags.Dashboard)]
    public record GetStatisticsQuery(
        // One car, or the whole fleet when null. The car list and a car's own page
        // link here with it set.
        int? CarId = null,
        StatisticsGranularity Granularity = StatisticsGranularity.Month,
        // Half-open [From, To), widened to whole buckets. Defaults to the current
        // calendar year by month, and to the last five years by year.
        DateTime? From = null,
        DateTime? To = null
    ) : IRequest<StatisticsDto>;

    public class GetStatisticsQueryHandler : IRequestHandler<GetStatisticsQuery, StatisticsDto>
    {
        // Ceilings on the number of rows. Three years of months and a decade of
        // years are both more history than a table is read down, and they bound
        // what one request can ask the database for.
        private const int MaxMonthBuckets = 36;
        private const int MaxYearBuckets = 10;

        // What a window defaults to when the caller names no end of it.
        private const int DefaultYearBuckets = 5;

        // The calendar the report can walk (see Clamp).
        private static readonly DateTime EarliestBound = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime LatestBound = new(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;
        private readonly IAgencySettingsProvider _settings;
        private readonly TimeProvider _dateTime;

        public GetStatisticsQueryHandler(
            IApplicationDbContext context, ITenantProvider tenant,
            IAgencySettingsProvider settings, TimeProvider dateTime)
        {
            _context = context;
            _tenant = tenant;
            _settings = settings;
            _dateTime = dateTime;
        }

        public async Task<StatisticsDto> Handle(
            GetStatisticsQuery request, CancellationToken cancellationToken)
        {
            var agencyId = _tenant.AgencyId ?? throw new UnauthorizedAccessException();
            var currency = (await _settings.GetAsync(agencyId, cancellationToken)).CurrencyCode;

            var now = _dateTime.GetUtcNow().UtcDateTime;
            var granularity = request.Granularity;

            var (from, to, truncated) = ResolveWindow(request.From, request.To, granularity, now);

            var carId = request.CarId;

            // --- The rows every figure is folded from -----------------------------
            //
            // Hires come back one row each rather than pre-aggregated in SQL: the
            // rented-days figure needs each hire's own pair of dates, and once the
            // rows are here the per-period and per-car folds below are the same
            // two passes over them. The window is capped (above) and an agency's
            // hires inside it are counted in hundreds, not millions.
            var hires = await _context.Rentings
                .Where(r => r.StartDate != null
                            && r.StartDate >= from && r.StartDate < to
                            && (carId == null || r.CarId == carId))
                .Select(r => new HireRow(
                    r.CarId,
                    r.StartDate!.Value,
                    r.EndDate,
                    r.RentingState,
                    r.Price == null ? 0m : r.Price.Amount,
                    r.CancellationFee == null ? 0m : r.CancellationFee.Amount))
                .ToListAsync(cancellationToken);

            // Only hire-linked payments: money a client hands over against their
            // account with no booking named cannot be credited to a vehicle. The
            // renting is joined for its car, so the figure follows the same
            // attribution as everything else on this screen.
            var collected = await _context.Payments
                .Where(p => p.PayementAmount != null
                            && p.RentingId != null
                            && p.PayementDate >= from && p.PayementDate < to
                            && (carId == null || p.Renting!.CarId == carId))
                .GroupBy(p => new
                {
                    p.PayementDate!.Value.Year,
                    p.PayementDate!.Value.Month,
                    CarId = p.Renting!.CarId
                })
                .Select(g => new MoneyRow(
                    g.Key.Year, g.Key.Month, g.Key.CarId, g.Sum(p => p.PayementAmount!.Amount)))
                .ToListAsync(cancellationToken);

            var expenses = await _context.Expenses
                .Where(e => e.ExpenseAmount != null
                            && e.ExpenseDate >= from && e.ExpenseDate < to
                            && (carId == null || e.CarId == carId))
                .GroupBy(e => new { e.ExpenseDate.Year, e.ExpenseDate.Month, e.CarId })
                .Select(g => new MoneyRow(
                    g.Key.Year, g.Key.Month, g.Key.CarId, g.Sum(e => e.ExpenseAmount!.Amount)))
                .ToListAsync(cancellationToken);

            // The fleet: the picker's options, the per-car rows' labels, and the
            // filtered screen's title all read off this one list.
            var cars = await _context.Cars
                .OrderBy(c => c.Matricule)
                .Select(c => new StatisticsCarOptionDto(
                    c.Id, c.Matricule, c.Model == null ? null : c.Model.Name))
                .ToListAsync(cancellationToken);

            // --- Period rows -----------------------------------------------------
            var periods = new List<StatisticsRowDto>();

            for (var bucketStart = from; bucketStart < to; bucketStart = Advance(bucketStart, granularity, 1))
            {
                var bucketEnd = Advance(bucketStart, granularity, 1);

                periods.Add(Row(
                    hires.Where(h => h.Start >= bucketStart && h.Start < bucketEnd),
                    SumMoney(collected, bucketStart, bucketEnd),
                    SumMoney(expenses, bucketStart, bucketEnd),
                    currency,
                    bucketStart: bucketStart,
                    bucketEnd: bucketEnd));
            }

            // --- Per-car rows ----------------------------------------------------
            // Only for the fleet view; a request already narrowed to one car has
            // its answer in the period rows above. Every car is listed, activity or
            // not: a vehicle that earned nothing all year is exactly what the
            // reader of this table is looking for.
            var byCar = new List<StatisticsRowDto>();

            if (carId == null)
            {
                foreach (var car in cars)
                {
                    byCar.Add(Row(
                        hires.Where(h => h.CarId == car.Id),
                        collected.Where(m => m.CarId == car.Id).Sum(m => m.Total),
                        expenses.Where(m => m.CarId == car.Id).Sum(m => m.Total),
                        currency,
                        car: car));
                }

                // Best earner first — the order the comparison is read in. Ties fall
                // back to the plate so the table has a stable order run to run.
                byCar = byCar
                    .OrderByDescending(r => r.Net!.Amount)
                    .ThenByDescending(r => r.Charged!.Amount)
                    .ThenBy(r => r.Matricule)
                    .ToList();

                // Whatever cannot be pinned to a car in the list — a hire recorded
                // without a vehicle, or money on one since removed from the fleet —
                // becomes one residual row at the bottom. The two tables sit on the
                // same screen, so they have to add up to the same totals rather than
                // quietly disagreeing by the odd retired car.
                var known = cars.Select(c => c.Id).ToHashSet();
                var strayHires = hires.Where(h => h.CarId == null || !known.Contains(h.CarId.Value)).ToList();
                var strayCollected = collected
                    .Where(m => m.CarId == null || !known.Contains(m.CarId.Value)).Sum(m => m.Total);
                var strayExpenses = expenses
                    .Where(m => m.CarId == null || !known.Contains(m.CarId.Value)).Sum(m => m.Total);

                if (strayHires.Count > 0 || strayCollected != 0m || strayExpenses != 0m)
                    byCar.Add(Row(strayHires, strayCollected, strayExpenses, currency));
            }

            var totals = Row(
                hires,
                collected.Sum(m => m.Total),
                expenses.Sum(m => m.Total),
                currency);

            var filtered = carId == null ? null : cars.FirstOrDefault(c => c.Id == carId);

            return new StatisticsDto
            {
                Currency = currency,
                From = from,
                To = to,
                Granularity = granularity,
                CarId = carId,
                CarLabel = filtered == null
                    ? null
                    : string.Join(" · ", new[] { filtered.Matricule, filtered.ModelName }
                        .Where(part => !string.IsNullOrWhiteSpace(part))),
                Truncated = truncated,
                Totals = totals,
                Periods = periods,
                ByCar = byCar,
                Cars = cars,
            };
        }

        /// <summary>
        /// The six figures of one slice.
        /// <para>
        /// ATTRIBUTION: a hire belongs WHOLE to the period it starts in — its days
        /// and its price are not split across the months it spans. That is what
        /// makes a row self-consistent ("3 hires, 21 days, 4 200") and it matches
        /// the dashboard's charged figure, so the two screens never disagree about
        /// what a month earned. Money moving (payments, expenses) is dated by when
        /// it moved, which is the only date those records have.
        /// </para>
        /// </summary>
        private static StatisticsRowDto Row(
            IEnumerable<HireRow> hires, decimal collected, decimal expenses, string currency,
            DateTime? bucketStart = null, DateTime? bucketEnd = null,
            StatisticsCarOptionDto? car = null)
        {
            var live = hires.Where(h => h.State != RentingState.Cancelled).ToList();
            var charged = hires.Sum(h => h.Charge);

            return new StatisticsRowDto
            {
                BucketStart = bucketStart,
                BucketEnd = bucketEnd,
                CarId = car?.Id,
                Matricule = car?.Matricule,
                ModelName = car?.ModelName,
                Rentings = live.Count,
                RentedDays = live.Sum(h => h.Days),
                Charged = new MoneyDto(charged, currency),
                Collected = new MoneyDto(collected, currency),
                Expenses = new MoneyDto(expenses, currency),
                Net = new MoneyDto(charged - expenses, currency),
            };
        }

        private static decimal SumMoney(IEnumerable<MoneyRow> rows, DateTime start, DateTime end) =>
            rows.Where(r => r.On >= start && r.On < end).Sum(r => r.Total);

        /// <summary>
        /// The window the rows cover: whole buckets, at most the ceiling many, and
        /// the LATEST of them when the request asked for more (recent months are
        /// what a report is opened for).
        /// </summary>
        private static (DateTime From, DateTime To, bool Truncated) ResolveWindow(
            DateTime? requestedFrom, DateTime? requestedTo,
            StatisticsGranularity granularity, DateTime now)
        {
            var max = granularity == StatisticsGranularity.Year ? MaxYearBuckets : MaxMonthBuckets;

            // Nothing asked for: this calendar year so far month by month, the last
            // five years year by year — the two reports an agency actually opens.
            var defaultTo = Advance(Truncate(now, granularity), granularity, 1);
            var defaultFrom = granularity == StatisticsGranularity.Year
                ? Advance(defaultTo, granularity, -DefaultYearBuckets)
                : new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var from = Truncate(Clamp(requestedFrom) ?? defaultFrom, granularity);

            var end = Clamp(requestedTo);

            DateTime to;

            if (end == null)
            {
                // A start on its own runs to the end of the current bucket: "since
                // January" means up to today, not up to January.
                to = requestedFrom == null ? defaultTo : Advance(Truncate(now, granularity), granularity, 1);
            }
            else
            {
                // The end is exclusive, so a bound landing inside a bucket includes
                // that whole bucket rather than dropping it.
                to = Advance(Truncate(end.Value.AddTicks(-1), granularity), granularity, 1);
            }

            // An inverted or empty window becomes a single bucket rather than an
            // error: a screen sending a bad pair gets one empty row, not a banner.
            if (to <= from) to = Advance(from, granularity, 1);

            var buckets = Buckets(from, to, granularity);
            if (buckets <= max) return (from, to, false);

            return (Advance(to, granularity, -max), to, true);
        }

        /// <summary>
        /// Keeps a requested bound inside the calendar the arithmetic below can walk.
        /// A hand-edited URL can name year 1 or year 9999, and stepping a month off
        /// either end throws rather than answering — a bound nobody could mean is
        /// pulled back to one the report can express.
        /// </summary>
        private static DateTime? Clamp(DateTime? value) => value == null
            ? null
            : value < EarliestBound ? EarliestBound
            : value > LatestBound ? LatestBound
            : value;

        private static int Buckets(DateTime from, DateTime to, StatisticsGranularity granularity) =>
            granularity == StatisticsGranularity.Year
                ? to.Year - from.Year
                : (to.Year - from.Year) * 12 + to.Month - from.Month;

        /// <summary>The start of the calendar bucket a moment falls in.</summary>
        private static DateTime Truncate(DateTime value, StatisticsGranularity granularity) =>
            granularity == StatisticsGranularity.Year
                ? new DateTime(value.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                : new DateTime(value.Year, value.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        private static DateTime Advance(DateTime value, StatisticsGranularity granularity, int steps) =>
            granularity == StatisticsGranularity.Year ? value.AddYears(steps) : value.AddMonths(steps);

        /// <summary>
        /// One hire, as SQL hands it over. <see cref="Charge"/> applies the app's
        /// charge rule (see ClientCreditRows): a cancelled hire bills the fee the
        /// agency kept, not the price it would have earned.
        /// </summary>
        private sealed record HireRow(
            int? CarId, DateTime Start, DateTime? End, RentingState State,
            decimal Price, decimal CancellationFee)
        {
            public decimal Charge => State == RentingState.Cancelled ? CancellationFee : Price;

            /// <summary>
            /// Calendar days the hire runs, a part day counting as a day and a hire
            /// with no end date counting as one — the same reading the counter gives
            /// a rental agreement. Never zero, so a same-day hire is not free days.
            /// </summary>
            public int Days => End == null
                ? 1
                : Math.Max(1, (int)Math.Ceiling((End.Value - Start).TotalDays));
        }

        /// <summary>Money grouped by calendar month and car, straight out of SQL.</summary>
        private sealed record MoneyRow(int Year, int Month, int? CarId, decimal Total)
        {
            public DateTime On => new(Year, Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }
    }
}
