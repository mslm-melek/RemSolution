using RemSolution.Application.Common.Models;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Car.DTOs
{
    /// <summary>
    /// The reading half of one car's page, in one answer: how hard the vehicle has
    /// been working, what it has earned, what customers made of it, who has it
    /// booked, and what it has cost lately.
    /// <para>
    /// Separate from <see cref="CarDto"/> rather than folded into it because that
    /// DTO is also a LIST row — the fleet screen projects fifty of them per page,
    /// and none of the figures below survive being computed fifty times over. The
    /// car's own facts stay on <see cref="CarDto"/>; this is only what the detail
    /// page adds on top.
    /// </para>
    /// <para>
    /// Reaching the page at all takes <c>Car.Read</c>, so the car is always
    /// answered for. Each SECTION below is gated on the module it reads and comes
    /// back null when the caller cannot reach it — the same rule the TODAY payload
    /// applies, so the page draws no card that would lead somewhere forbidden.
    /// </para>
    /// </summary>
    public class CarOverviewDto
    {
        public int CarId { get; init; }
        public string Currency { get; init; } = string.Empty;

        /// <summary>
        /// The half-open window <see cref="Usage"/> covers, as UTC midnights — the
        /// screen names it rather than hard-coding "90 days" in three languages.
        /// </summary>
        public DateTime From { get; init; }
        public DateTime To { get; init; }

        /// <summary>How busy the car has been, and what that billed. Needs Rentings.</summary>
        public CarUsageDto? Usage { get; init; }

        /// <summary>
        /// What customers scored this car, across every review left on one of its
        /// hires. Never null — reviews are public content the agency always owns
        /// (see AgencyReview) — but <see cref="CarRatingDto.Count"/> is zero for a
        /// car nobody has rated, and the screen shows no tile for that.
        /// </summary>
        public CarRatingDto Rating { get; init; } = new();

        /// <summary>
        /// The hire running now and the ones booked next, soonest first. Null
        /// without Rentings; empty means the car is free and unspoken for, which is
        /// a different sentence on screen.
        /// </summary>
        public IList<CarBookingDto>? Bookings { get; init; }

        /// <summary>
        /// Hires this car has had, cancelled ones excluded — how many the "view
        /// all" link leads to. Null without Rentings.
        /// </summary>
        public int? BookingsTotal { get; init; }

        /// <summary>Latest costs booked against the car, newest first. Null without Expenses.</summary>
        public IList<CarExpenseDto>? Expenses { get; init; }

        /// <summary>What the car cost over <see cref="From"/>..<see cref="To"/>. Null without Expenses.</summary>
        public MoneyDto? ExpensesTotal { get; init; }
    }

    /// <summary>How much of the window the car spent out, and what it billed.</summary>
    public class CarUsageDto
    {
        /// <summary>
        /// Calendar days in the window on which the car was out on hire. Counted by
        /// DAY OCCUPIED, not by hire: two bookings sharing a day are one day out,
        /// so the figure can never exceed the window and the percentage below is
        /// always a percentage. Cancelled hires never held the car and are excluded.
        /// </summary>
        public int RentedDays { get; init; }

        /// <summary>Days in the window — the denominator, so the screen can show both.</summary>
        public int WindowDays { get; init; }

        /// <summary>
        /// <see cref="RentedDays"/> / <see cref="WindowDays"/> as a whole
        /// percentage, rounded — computed here so the tile, the tooltip and any
        /// future export all read the same number.
        /// </summary>
        public int UtilizationPercent { get; init; }

        /// <summary>
        /// What the car billed over the window: the agreed price of the hires that
        /// STARTED in it, plus the fee kept on any that was cancelled.
        /// </summary>
        /// <remarks>
        /// Deliberately the statistics report's attribution (a hire belongs whole
        /// to the period it starts in), not the occupancy rule above: a figure this
        /// tile shows and that report shows must be the same figure, and the report
        /// is the one an agency reconciles against. The two rules answer different
        /// questions — "was the car working?" versus "what did it bill?" — which is
        /// why one screen can want both.
        /// </remarks>
        public MoneyDto? Charged { get; init; }

        /// <summary>Hires that started in the window, cancelled ones excluded.</summary>
        public int Rentings { get; init; }
    }

    /// <summary>A car's public score, folded from the reviews on its hires.</summary>
    public class CarRatingDto
    {
        public int Count { get; init; }

        /// <summary>Mean rating, 1–5, to one decimal. Null when nobody has rated it.</summary>
        public double? Average { get; init; }
    }

    /// <summary>
    /// One booking on this car as the page's compact list shows it — enough to
    /// recognise it and act on it, not the whole renting (that is one click away).
    /// </summary>
    public class CarBookingDto
    {
        public int RentingId { get; init; }
        public int? ClientId { get; init; }
        public string? ClientName { get; init; }

        /// <summary>
        /// How to reach them, which is what the desk wants off this row. Often
        /// null: an email is only recorded when the client is given a portal login.
        /// </summary>
        public string? ClientEmail { get; init; }

        public DateTime? StartDate { get; init; }
        public DateTime? EndDate { get; init; }
        public RentingState State { get; init; }
        public MoneyDto? Price { get; init; }

        /// <summary>
        /// Still out, and due back before now. Decided server-side against the
        /// server's clock for the same reason the booking calendar decides it there:
        /// a browser an hour out would colour rows the API never called late.
        /// </summary>
        public bool IsLate { get; init; }
    }

    /// <summary>One cost booked against the car, as the page's compact list shows it.</summary>
    public class CarExpenseDto
    {
        public int Id { get; init; }
        public string? TypeName { get; init; }
        public string? Description { get; init; }
        public DateTime ExpenseDate { get; init; }
        public MoneyDto? Amount { get; init; }

        /// <summary>Whether the agency still owes money on it — the row's one flag.</summary>
        public bool IsUnpaid { get; init; }
    }
}
