using RemSolution.Application.Common.Models;

namespace RemSolution.Application.Features.Credit.DTOs
{
    // What one client still owes the agency: everything they have been charged
    // for (rentings plus reservations still representing an obligation) against
    // everything they have paid, net of refunds and reversals. Same arithmetic as
    // ClientBalanceDto, listed for the whole agency instead of one client.
    public class ClientCreditDto
    {
        public int ClientId { get; init; }
        public string? ClientName { get; init; }
        public string? CIN { get; init; }
        public MoneyDto? Charged { get; init; }
        public MoneyDto? Paid { get; init; }
        // Charged − Paid: positive = the client owes; negative = they are in credit.
        public MoneyDto? Outstanding { get; init; }
        // Rentings not yet finished (upcoming or ongoing) — the ones that will
        // still generate charges.
        public int OpenRentingCount { get; init; }
    }

    // What the agency still owes on one booked expense. The mirror image of
    // ClientCreditDto: money going out rather than coming in.
    public class ExpenseCreditDto
    {
        public int ExpenseId { get; init; }
        public int CarId { get; init; }
        public string? CarMatricule { get; init; }
        public string? ExpenseTypeName { get; init; }
        public DateTime ExpenseDate { get; init; }
        public MoneyDto? Amount { get; init; }
        public MoneyDto? Paid { get; init; }
        public MoneyDto? Outstanding { get; init; }
        public string? Description { get; init; }
        // The payable tab is where expenses are managed now (the standalone
        // expense list is gone), so the row carries what its actions need: the
        // type to re-filter by, and the attached invoice to show or replace.
        public int ExpenseTypeId { get; init; }
        public string? FactureFileUrl { get; init; }
        public string? FactureFileName { get; init; }
    }

    // Both sides of the agency's credit position in one figure set, so the screen
    // can show totals without paging through every row.
    public class CreditsSummaryDto
    {
        public string Currency { get; init; } = string.Empty;
        // Owed TO the agency by its clients (sum of positive client balances;
        // clients in credit are excluded so overpayments do not mask real debt).
        public MoneyDto? ClientsOutstanding { get; init; }
        public MoneyDto? ClientsCharged { get; init; }
        public MoneyDto? ClientsPaid { get; init; }
        public int ClientsInDebtCount { get; init; }
        // Owed BY the agency on its booked expenses.
        public MoneyDto? ExpensesOutstanding { get; init; }
        public MoneyDto? ExpensesTotal { get; init; }
        public MoneyDto? ExpensesPaid { get; init; }
        public int UnpaidExpenseCount { get; init; }
        // ClientsOutstanding − ExpensesOutstanding: what the agency is net owed.
        public MoneyDto? Net { get; init; }
    }
}
