using RemSolution.Application.Common.Models;

namespace RemSolution.Application.Features.Dashboard.DTOs
{
    // How the trend series is bucketed. The window the figures cover is picked
    // separately (From/To), so "this quarter by day" and "five years by year" are
    // both expressible: the window says what is measured, this says how finely.
    public enum DashboardGranularity
    {
        Day = 1,
        Month = 2,
        Year = 3,
    }

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
        public int NewCarsInPeriod { get; init; }

        // --- Bookings ---
        public int RentingsInProgress { get; init; }
        public int RentingsUpcoming { get; init; }
        // Reservation requests waiting on the agency to accept or refuse.
        public int PendingReservationRequests { get; init; }
        // Ongoing rentings whose end date falls inside the period — the returns
        // the desk still has to handle.
        public int ReturnsDueInPeriod { get; init; }
        // Rentings whose hire started inside the period.
        public int RentingsStartedInPeriod { get; init; }

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

        // --- Trend ---
        // The bucket size the series below uses, echoed back so the screen can
        // label the points without having to remember what it asked for.
        public DashboardGranularity Granularity { get; init; }
        // Trailing series ending with the period's last bucket, oldest first, with
        // empty buckets emitted as zeroes — enough for the screen to draw fleet,
        // client and money trends from one call.
        public IList<DashboardPeriodPointDto> Series { get; init; } = new List<DashboardPeriodPointDto>();
    }

    // One bucket of the trend series. Carries the fleet, client and money figures
    // together: they are read off the same chart, and splitting them across calls
    // would let the three drift out of step while the user changes the window.
    public class DashboardPeriodPointDto
    {
        // Half-open [BucketStart, BucketEnd), like every other window in the app.
        public DateTime BucketStart { get; init; }
        public DateTime BucketEnd { get; init; }

        // Cars and clients added in the bucket. Deliberately additions rather
        // than a stock figure: the fleet "as it was" cannot be reconstructed from
        // live rows once a car is archived, so a stock line would quietly rewrite
        // history every time one is deleted.
        public int NewCars { get; init; }
        public int NewClients { get; init; }
        // Rentings whose hire started in the bucket — the activity figure that
        // makes the fleet numbers mean something.
        public int RentingsStarted { get; init; }

        public MoneyDto? Collected { get; init; }
        public MoneyDto? Expenses { get; init; }
    }
}
