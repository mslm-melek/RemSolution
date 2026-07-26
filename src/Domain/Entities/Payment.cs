namespace RemSolution.Domain.Entities
{
    public class Payment : BaseAuditableEntity, ITenantEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        public int? ClientId { get; set; }
        public virtual Client? Client { get; set; }
        // What this payment settles. A payment targets a renting, a reservation,
        // or the client directly — at most one booking FK is set, and the client
        // is always known. Restrict on delete — a renting/reservation is a
        // financial record and is never physically removed anyway.
        public int? RentingId { get; set; }
        public virtual Renting? Renting { get; set; }
        public int? ReservationId { get; set; }
        public virtual Reservation? Reservation { get; set; }
        public DateTime? PayementDate { get; set; }
        public Money? PayementAmount { get; set; }
        public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
        // A refund returns money to the client (recorded as a negative amount),
        // as opposed to a reversal (below), which corrects a mistaken entry.
        public bool IsRefund { get; set; }
        public string? Notes { get; set; }
        // A payment is never deleted; a mistaken one is reversed by an offsetting
        // entry that points back at the original via this self-reference.
        public int? ReversesPaymentId { get; set; }
        public virtual Payment? ReversesPayment { get; set; }
    }
}
