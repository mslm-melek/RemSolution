using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.Features.Renting.Booking
{
    /// <summary>
    /// One extra charge as a caller sends it. Shared by the return (which books
    /// its fees in the same write that closes the hire) and by the standalone
    /// add, so both state the same fields and are validated the same way.
    /// </summary>
    public record RentingFeePayload
    {
        public RentingFeeKind Kind { get; init; }

        /// <summary>Positive; the currency is the hire's, never the caller's.</summary>
        public decimal Amount { get; init; }

        public string? Note { get; init; }
    }

    public class RentingFeePayloadValidator : AbstractValidator<RentingFeePayload>
    {
        public RentingFeePayloadValidator()
        {
            RuleFor(v => v.Kind).IsInEnum();
            RuleFor(v => v.Amount).GreaterThan(0);
            RuleFor(v => v.Note).MaximumLength(500);
        }
    }

    public static class RentingFees
    {
        /// <summary>
        /// The currency an extra charge is booked in: the hire's own when it has a
        /// price, the agency's otherwise — the same fallback the quote uses, so a
        /// courtesy car with no rate can still be charged for its dents.
        /// </summary>
        public static string CurrencyOf(Domain.Entities.Renting renting, string agencyCurrency) =>
            renting.Price?.Currency ?? agencyCurrency;

        /// <summary>
        /// Books a batch through the aggregate, which is where the rules live (see
        /// Renting.AddFee).
        /// </summary>
        public static void AddAll(
            Domain.Entities.Renting renting,
            IEnumerable<RentingFeePayload>? fees,
            string currency)
        {
            foreach (var fee in fees ?? Enumerable.Empty<RentingFeePayload>())
            {
                renting.AddFee(fee.Kind, Money.Of(fee.Amount, currency), fee.Note);
            }
        }
    }
}
