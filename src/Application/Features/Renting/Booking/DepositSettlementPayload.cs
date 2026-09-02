using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
// The feature namespaces include a Payment one, so the entity is aliased rather
// than imported — the same trick CreateReservationCommand uses for Reservation.
using PaymentEntity = RemSolution.Domain.Entities.Payment;

namespace RemSolution.Application.Features.Renting.Booking
{
    /// <summary>
    /// What becomes of the deposit, as a caller sends it. Shared by the return
    /// (which settles in the same write that closes the hire) and by the
    /// standalone settle, so a deposit dealt with a week later goes through
    /// exactly the same arithmetic.
    /// </summary>
    public record DepositSettlementPayload
    {
        /// <summary>
        /// How much the agency keeps — towards the damage, the missing fuel, the
        /// late day. Zero returns the whole deposit, which is the common case and
        /// still a decision worth recording.
        /// </summary>
        public decimal RetainedAmount { get; init; }

        /// <summary>How the refund was handed back. Ignored when nothing is refunded.</summary>
        public PaymentMethod RefundMethod { get; init; } = PaymentMethod.Cash;

        public string? Note { get; init; }
    }

    public class DepositSettlementPayloadValidator : AbstractValidator<DepositSettlementPayload>
    {
        public DepositSettlementPayloadValidator()
        {
            RuleFor(v => v.RetainedAmount).GreaterThanOrEqualTo(0);
            RuleFor(v => v.RefundMethod).IsInEnum();
            RuleFor(v => v.Note).MaximumLength(500);
        }
    }

    public static class DepositSettlements
    {
        /// <summary>
        /// Records the decision on the aggregate and returns the refund entry the
        /// caller must add, or null when the agency kept the whole deposit.
        /// <para>
        /// The refund is a <see cref="Payment"/> and not a field, because the
        /// client's balance is computed from the payment ledger (see
        /// ClientCreditRows): a deposit returned without a ledger entry would
        /// leave the client looking as though they had paid money they no longer
        /// have. AgencyId is stamped by the tenant interceptor on insert.
        /// </para>
        /// </summary>
        public static PaymentEntity? Settle(
            Domain.Entities.Renting renting,
            DepositSettlementPayload settlement,
            DateTime settledAt)
        {
            var deposit = renting.DepositAmount
                ?? throw new InvalidOperationException(
                    "Settle must not be called on a hire that took no deposit; check HasUnsettledDeposit.");

            var retained = Money.Of(settlement.RetainedAmount, deposit.Currency);

            // Throws on a retention above the deposit or in the wrong currency.
            renting.SettleDeposit(retained, settledAt);

            var refunded = deposit.Amount - retained.Amount;

            if (refunded <= 0m)
            {
                return null;
            }

            return new PaymentEntity
            {
                ClientId = renting.ClientId,
                RentingId = renting.Id,
                // Truncated like every other server-side default, so a settlement
                // date has the same shape as a backdated one (see the time note
                // in docs/PROJECT_OVERVIEW.md).
                PayementDate = settledAt.Date,
                // Negative: a refund moves money the other way, and the ledger is
                // summed rather than split by sign.
                PayementAmount = Money.Of(-refunded, deposit.Currency),
                Method = settlement.RefundMethod,
                IsRefund = true,
                Notes = settlement.Note,
            };
        }
    }
}
