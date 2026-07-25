namespace RemSolution.Domain.Entities
{
    public class Payment : BaseAuditableEntity, ITenantEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        public int? ClientId { get; set; }
        public virtual Client? Client { get; set; }
        // The renting this payment settles. Restrict on delete — a renting is a
        // financial record and is never physically removed anyway.
        public int? RentingId { get; set; }
        public virtual Renting? Renting { get; set; }
        public DateTime? PayementDate { get; set; }
        public Money? PayementAmount { get; set; }
        public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
        public string? Notes { get; set; }
        // A payment is never deleted; a mistaken one is reversed by an offsetting
        // entry that points back at the original via this self-reference.
        public int? ReversesPaymentId { get; set; }
        public virtual Payment? ReversesPayment { get; set; }
    }
}
