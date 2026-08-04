using RemSolution.Application.Common.Models;
using RemSolution.Application.Features.Credit.DTOs;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Credit.Queries
{
    // One client's receivable position as SQL computes it: bare decimals, before
    // the agency's single currency is put back on them (see Money). Shared so the
    // credits list and the per-client lookups cannot drift apart on what "charged"
    // means — the arithmetic below is the same one ClientBalanceDto uses.
    public class ClientCreditRow
    {
        public int ClientId { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string? CIN { get; init; }
        public decimal Charged { get; init; }
        public decimal Paid { get; init; }
        public int OpenRentingCount { get; init; }
    }

    public static class ClientCreditRows
    {
        /// <summary>
        /// <para>
        /// THE charge rule, in one place. What a renting charges its client is its
        /// agreed <c>Price</c> — unless it was cancelled, in which case it charges
        /// its <c>CancellationFee</c>: nothing when the agency called it off for
        /// free, or the part of the price it kept (see Renting.CancellationFee).
        /// Reservations charge their price while they are still an obligation; a
        /// converted one has become a renting and would otherwise count twice.
        /// Paid is the net of the ledger, so refunds and reversals subtract
        /// themselves.
        /// </para>
        /// <para>
        /// Every screen that shows what clients owe reads it from here — the
        /// credits list and its by-ids lookup, the credits summary tiles, the
        /// dashboard's debtor figures and one client's balance — because four
        /// copies of this arithmetic is four chances for the same client to owe
        /// four different amounts. RentingDto.Outstanding and the ceiling
        /// CreatePaymentCommand enforces apply the same rule per booking.
        /// </para>
        /// </summary>
        public static IQueryable<ClientCreditRow> ToCreditRows(
            this IQueryable<Domain.Entities.Client> clients) =>
            clients.Select(c => new ClientCreditRow
            {
                ClientId = c.Id,
                FirstName = c.FirstName,
                LastName = c.LastName,
                CIN = c.CIN,
                Charged =
                    c.Rentings!
                        .Sum(r => r.RentingState == RentingState.Cancelled
                            ? (r.CancellationFee == null ? 0m : r.CancellationFee.Amount)
                            : (r.Price == null ? 0m : r.Price.Amount))
                    + c.Reservations!
                        .Where(r => (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.Paid)
                                    && r.Price != null)
                        .Sum(r => r.Price!.Amount),
                Paid = c.Payments!
                    .Where(p => p.PayementAmount != null)
                    .Sum(p => p.PayementAmount!.Amount),
                OpenRentingCount = c.Rentings!
                    .Count(r => r.RentingState == RentingState.NotYet
                                || r.RentingState == RentingState.InProgress),
            });

        public static ClientCreditDto ToDto(this ClientCreditRow row, string currency) =>
            new()
            {
                ClientId = row.ClientId,
                ClientName = ((row.FirstName ?? string.Empty) + " " + (row.LastName ?? string.Empty)).Trim(),
                CIN = row.CIN,
                Charged = new MoneyDto(row.Charged, currency),
                Paid = new MoneyDto(row.Paid, currency),
                Outstanding = new MoneyDto(row.Charged - row.Paid, currency),
                OpenRentingCount = row.OpenRentingCount,
            };
    }
}
