using RemSolution.Application.Common.Models;

namespace RemSolution.Application.Features.Payment.DTOs
{
    // The financial position of one client with the agency: what they have been
    // charged (across active rentings and confirmed reservations), what they have
    // paid (net of refunds/reversals), and the outstanding balance between them.
    public class ClientBalanceDto
    {
        public int ClientId { get; init; }
        public string? ClientName { get; init; }
        public string Currency { get; init; } = string.Empty;
        public MoneyDto? TotalCharged { get; init; }
        public MoneyDto? TotalPaid { get; init; }
        // Charged − Paid: positive = the client still owes; negative = in credit.
        public MoneyDto? Balance { get; init; }
    }
}
