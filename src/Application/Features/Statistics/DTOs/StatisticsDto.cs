using RemSolution.Application.Common.Models;

namespace RemSolution.Application.Features.Statistics.DTOs
{
    /// <summary>
    /// How the statistics rows are sliced. Deliberately coarser than the
    /// dashboard's granularity (which also offers days): this screen answers "how
    /// did this car do last month / last year", and a day-by-day fleet report is
    /// the booking calendar's job, not a statistics table's.
    /// </summary>
    public enum StatisticsGranularity
    {
        Month = 1,
        Year = 2,
    }

    /// <summary>
    /// One agency's rental and money figures over a window, sliced by period and
    /// by car. Read-only; every figure comes from the tenant-filtered sets.
    /// </summary>
    public class StatisticsDto
    {
        public string Currency { get; init; } = string.Empty;

        // Half-open [From, To), aligned to whole buckets: asking for the 12th of
        // March by month gives the whole of March, so a row is never a part-month
        // the reader has to discount.
        public DateTime From { get; init; }
        public DateTime To { get; init; }
        public StatisticsGranularity Granularity { get; init; }

        // The car the figures are restricted to, echoed back with its label so the
        // screen can title itself without a second call. Null = the whole fleet.
        public int? CarId { get; init; }
        public string? CarLabel { get; init; }

        // Set when the requested window held more buckets than the query returns
        // (the latest ones are kept). The screen says so rather than quietly
        // showing a shorter history than was asked for.
        public bool Truncated { get; init; }

        /// <summary>The whole window in one row — the figures the tiles show.</summary>
        public StatisticsRowDto Totals { get; init; } = new();

        /// <summary>
        /// One row per bucket, oldest first, empty buckets emitted as zeroes so the
        /// table has a line for every month/year in the window.
        /// </summary>
        public IList<StatisticsRowDto> Periods { get; init; } = new List<StatisticsRowDto>();

        /// <summary>
        /// One row per car over the WHOLE window — which vehicle earns and which
        /// only costs. Empty when the request already filters to a single car:
        /// the period rows above are then that car's breakdown.
        /// <para>
        /// The rows add up to <see cref="Totals"/>: anything belonging to no car in
        /// the fleet today (a hire with no vehicle, a car since removed) comes back
        /// as a last row with no <c>CarId</c>.
        /// </para>
        /// </summary>
        public IList<StatisticsRowDto> ByCar { get; init; } = new List<StatisticsRowDto>();

        /// <summary>
        /// The fleet, for the screen's car picker. Always the full list, filter or
        /// not — the picker must still offer the other cars while one is selected,
        /// and it saves the screen a second call to the cars list.
        /// </summary>
        public IList<StatisticsCarOptionDto> Cars { get; init; } = new List<StatisticsCarOptionDto>();
    }

    /// <summary>
    /// One slice of the figures. The same shape serves the three tables — a period
    /// row carries its bucket, a car row carries its car, the totals row carries
    /// neither — because all three are read as the same six numbers, and three
    /// near-identical types would be three chances for one of them to mean
    /// something slightly different.
    /// </summary>
    public class StatisticsRowDto
    {
        // Half-open [BucketStart, BucketEnd). Set on period rows only.
        public DateTime? BucketStart { get; init; }
        public DateTime? BucketEnd { get; init; }

        // Set on per-car rows only.
        public int? CarId { get; init; }
        public string? Matricule { get; init; }
        public string? ModelName { get; init; }

        /// <summary>Hires that STARTED in the slice, cancelled ones excluded.</summary>
        public int Rentings { get; init; }

        /// <summary>
        /// Days those hires run for, counted whole against the slice their hire
        /// starts in (see the attribution note on the handler). A hire is at least
        /// one day, so a same-day rental is not a free one.
        /// </summary>
        public int RentedDays { get; init; }

        /// <summary>
        /// What those hires bill: the agreed price, plus the fee kept on any hire
        /// that was cancelled — the app's one charge rule (see ClientCreditRows).
        /// </summary>
        public MoneyDto? Charged { get; init; }

        /// <summary>
        /// Money actually banked in the slice against a hire of the car(s). Client
        /// payments not tied to a hire cannot be attributed to a vehicle and are
        /// left out, which is why this is not the dashboard's "collected".
        /// </summary>
        public MoneyDto? Collected { get; init; }

        /// <summary>What was spent on the car(s), by expense date.</summary>
        public MoneyDto? Expenses { get; init; }

        /// <summary>Charged − Expenses: what the car(s) earned before collection.</summary>
        public MoneyDto? Net { get; init; }
    }

    /// <summary>A car as the screen's filter lists it.</summary>
    public record StatisticsCarOptionDto(int Id, string? Matricule, string? ModelName);
}
