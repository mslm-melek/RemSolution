using RemSolution.Application.Common.Models;

namespace RemSolution.Application.Features.Dashboard.DTOs
{
    // One agency's at-a-glance figures: the client/fleet counts on the left of
    // the screen and the money figures on the right. Counts are all-time unless
    // named otherwise; every "InPeriod" figure covers the requested window
    // (current calendar month by default).
    public class DashboardDto
    {
        public string Currency { get; init; } = string.Empty;
        public DateTime PeriodStart { get; init; }
        public DateTime PeriodEnd { get; init; }

        // --- Clients ---
        public int TotalClients { get; init; }
        public int NewClientsInPeriod { get; init; }
        public int FlaggedClients { get; init; }
        public int ClientsInDebtCount { get; init; }

        // --- Fleet ---
        public int TotalCars { get; init; }
        public int ActiveCars { get; init; }
        // Cars with a renting currently in progress.
        public int CarsOnRent { get; init; }

        // --- Bookings ---
        public int RentingsInProgress { get; init; }
        public int RentingsUpcoming { get; init; }
        // Reservation requests waiting on the agency to accept or refuse.
        public int PendingReservationRequests { get; init; }
        // Ongoing rentings whose end date falls inside the period — the returns
        // the desk still has to handle.
        public int ReturnsDueInPeriod { get; init; }

        // --- Money ---
        // Agreed price of rentings starting inside the period.
        public MoneyDto? ChargedInPeriod { get; init; }
        // Payments actually banked inside the period, net of refunds/reversals.
        public MoneyDto? CollectedInPeriod { get; init; }
        // Expenses booked inside the period.
        public MoneyDto? ExpensesInPeriod { get; init; }
        // Collected − Expenses: the period's cash result.
        public MoneyDto? NetInPeriod { get; init; }
        // All-time, not period-scoped: what clients still owe (positive balances
        // only) and what the agency still owes on its expenses.
        public MoneyDto? ClientsOutstanding { get; init; }
        public MoneyDto? ExpensesOutstanding { get; init; }

        // Trailing series ending with the period's month, oldest first — enough
        // for the screen to draw a bar per month without a second call.
        public IList<DashboardMonthPointDto> MonthlySeries { get; init; } = new List<DashboardMonthPointDto>();
    }

    public class DashboardMonthPointDto
    {
        public int Year { get; init; }
        public int Month { get; init; }
        public MoneyDto? Collected { get; init; }
        public MoneyDto? Expenses { get; init; }
    }
}
