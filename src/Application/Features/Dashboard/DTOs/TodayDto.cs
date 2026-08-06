using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Notifications;

namespace RemSolution.Application.Features.Dashboard.DTOs
{
    // The desk's landing screen in one call: what today asks for, what is waiting
    // on somebody, and what the fleet is doing. Deliberately ONE payload rather
    // than the eight queries the screen used to make — every figure on it is read
    // together, and a screen assembled from eight answers shows eight different
    // moments in time.
    //
    // Every section is nullable, and null means "not yours to see": the module is
    // switched off for the agency, or the caller lacks its read permission. The
    // screen renders a section only when it is present, so a feature that is off
    // leaves no empty card behind (the same rule the navigation applies).
    public class TodayDto
    {
        public string Currency { get; init; } = string.Empty;

        // The calendar day the figures cover, as UTC midnight — the same wall-clock
        // stamping every other date in the API uses, so the screen reads it with
        // the UTC parts.
        public DateTime Day { get; init; }

        // The branch the figures were scoped to, echoed back, and the branches the
        // caller could pick instead. Empty when the agency has no branches (or the
        // Branches module is off), which is what hides the picker.
        public int? BranchId { get; init; }
        public IList<TodayBranchDto> Branches { get; init; } = new List<TodayBranchDto>();

        public TodayFleetDto? Fleet { get; init; }
        public TodaySummaryDto Summary { get; init; } = new();
        public TodayMoneyDto? Money { get; init; }

        // The two "needs your answer" cards. Null when their module is unreachable.
        public TodayRequestsDto? Requests { get; init; }
        public TodayPayablesDto? Payables { get; init; }

        // Recurring car costs coming due, grouped by the cost they are. Null when
        // the expense module (or the fleet) is unreachable; empty when nothing is
        // due, which is a different sentence on screen.
        public IList<TodayExpenseGroupDto>? ExpensesDue { get; init; }
    }

    public class TodayBranchDto
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    // How much of the fleet is standing on the forecourt right now.
    public class TodayFleetDto
    {
        public int Total { get; init; }
        // Bookable and not currently out: the figure the desk answers "have you got
        // a car?" with.
        public int Free { get; init; }
        public int OnRent { get; init; }
        // Off the road — in the garage or retired.
        public int OutOfService { get; init; }
    }

    // The four figures across the top of the screen. Each is null when the module
    // behind it is unreachable, so the screen drops that card rather than showing
    // a zero it never counted.
    public class TodaySummaryDto
    {
        // Hires and live holds starting today, and how many of those holds still
        // need a yes or a no.
        public int? BookingsToday { get; init; }
        public int? UnconfirmedToday { get; init; }

        // Hires running now whose return falls today, and how many of those are due
        // before midday — the morning's work.
        public int? ReturnsToday { get; init; }
        public int? ReturnsBeforeNoon { get; init; }

        // Hires still out that were due back before now.
        public int? LateRentings { get; init; }
        // The worst of them, to name on the card. Null when nothing is late.
        public TodayLateRentingDto? WorstLate { get; init; }
    }

    public class TodayLateRentingDto
    {
        public int RentingId { get; init; }
        public string? ClientName { get; init; }
        public string? CarLabel { get; init; }
        public DateTime DueOn { get; init; }
        // Whole hours since it was due, so the card can say "4 h" without doing
        // date arithmetic against a clock that may be a minute off the server's.
        public int HoursLate { get; init; }
    }

    public class TodayMoneyDto
    {
        // Agreed price of the hires starting today: what the desk expects to take
        // in over the counter. Dated by the hire's start, exactly as the overview
        // screen's "charged" figure is, so the two never disagree.
        public MoneyDto? ExpectedToday { get; init; }
        // All-time and not scoped to the day: what clients still owe.
        public MoneyDto? Outstanding { get; init; }
    }

    // Holds waiting for the agency to accept or refuse.
    public class TodayRequestsDto
    {
        public int Count { get; init; }
        // When the oldest of them was asked for, so the card can say how long
        // somebody has been waiting. Null when there are none.
        public DateTimeOffset? OldestAskedAt { get; init; }
        // The top of the queue, so the card can be answered without opening the
        // list (see the home screen's expandable request card).
        public IList<TodayRequestDto> Items { get; init; } = new List<TodayRequestDto>();
    }

    public class TodayRequestDto
    {
        public int ReservationId { get; init; }
        public int? ClientId { get; init; }
        public string? ClientName { get; init; }
        public int? CarId { get; init; }
        public string? CarLabel { get; init; }
        public DateTime? StartDate { get; init; }
        public DateTime? ExpiresAt { get; init; }
    }

    // Expenses the agency has booked but not settled.
    public class TodayPayablesDto
    {
        public int Count { get; init; }
        public MoneyDto? Outstanding { get; init; }
    }

    // One recurring cost, and the cars that owe it.
    public class TodayExpenseGroupDto
    {
        public int ExpenseTypeId { get; init; }
        public string Name { get; init; } = string.Empty;
        // The recurrence rule, for the card's subtitle ("every 10 000 km").
        public int? AfterMonth { get; init; }
        public int? AfterKilometer { get; init; }
        // True as soon as ONE of the cars is past its threshold: the group reads
        // as urgent, the individual rows say which of them actually are.
        public bool IsOverdue { get; init; }
        public IList<TodayExpenseCarDto> Cars { get; init; } = new List<TodayExpenseCarDto>();
    }

    // One car owing one recurring cost — the calculator's answer, flattened. Only
    // the triggering clock's figures are populated (see ExpenseDue).
    public class TodayExpenseCarDto
    {
        public int CarId { get; init; }
        public string? Matricule { get; init; }
        public string? ModelName { get; init; }

        public ExpenseDueBasis Basis { get; init; }
        public bool IsOverdue { get; init; }

        // Date clock: when it falls due, and the whole days to (or past) it.
        public DateTime? DueOn { get; init; }
        public int Days { get; init; }

        // Distance clock: the reading it falls due at, and the kilometres left
        // before (or covered past) it.
        public int? DueAtKilometers { get; init; }
        public int? Kilometers { get; init; }
    }
}
