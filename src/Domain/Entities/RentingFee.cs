using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// One extra charge established when the car came back — a late day, a dent,
    /// the kilometres over the allowance. Written only through
    /// <see cref="Renting.AddFee"/>, which is what keeps it in the hire's own
    /// currency and off a hire that never went out.
    /// <para>
    /// Not to be confused with an <see cref="ExtraService"/>: that is an option
    /// SOLD at the counter and invoiced beside the rental, while this is money
    /// the hire turned out to owe. It therefore adds to what the client owes (see
    /// ClientCreditRows), which an extra service does not.
    /// </para>
    /// </summary>
    public class RentingFee : BaseAuditableEntity, ITenantEntity
    {
        // Stamped by the tenant interceptor, like every other child row.
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }

        public int RentingId { get; private set; }
        public virtual Renting? Renting { get; private set; }

        public RentingFeeKind Kind { get; private set; }

        /// <summary>Always positive, and always in the hire's currency.</summary>
        public Money? Amount { get; private set; }

        /// <summary>What it was for, in the words of whoever took the car back.</summary>
        public string? Note { get; private set; }

        // EF materialisation.
        private RentingFee() { }

        // Only the aggregate builds these; see Renting.AddFee for the rules.
        internal RentingFee(RentingFeeKind kind, Money amount, string? note)
        {
            Kind = kind;
            Amount = amount;
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        }
    }
}
