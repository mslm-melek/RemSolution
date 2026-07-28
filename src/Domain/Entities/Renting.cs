namespace RemSolution.Domain.Entities
{
    public class Renting : BaseAuditableEntity, ITenantEntity, IHasRowVersion
    {
        // Optimistic-concurrency token; see IHasRowVersion.
        public byte[]? RowVersion { get; set; }
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        public int? CarId { get; set; }
        public virtual Car? Car { get; set; }
        public int? ClientId { get; set; }
        public virtual Client? Client { get; set; }
        public int? SecondClientId { get; set; }
        public virtual Client? SecondClient { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? StartMileage { get; set; }
        public int? EndMileage { get; set; }
        // The agreed price, snapshotted from IPricingService at creation time and
        // never silently re-read from the car afterwards (see IPricingService).
        public Money? Price { get; set; }
        // Refundable deposit held for the vehicle, carried over from the
        // reservation on conversion. Distinct from Price (the rental charge).
        public Money? DepositAmount { get; set; }
        public RentingState RentingState { get; set; }
        public string? Notes { get; set; }
        public virtual ICollection<ExtraService>? ExtraServices { get; set; }
        public virtual ICollection<RentingHistory>? RentingHistories { get; set; }
        public virtual ICollection<Reservation>? Reservations { get; set; }
        public virtual ICollection<Payment>? Payments { get; set; }
        // Generated paperwork. Append-only: regenerating issues a new numbered
        // document rather than replacing the previous one (see Contract/Facture).
        public virtual ICollection<Contract>? Contracts { get; set; }
        public virtual ICollection<Facture>? Factures { get; set; }
        // The renting doubles as the agency ⇄ client conversation thread; see
        // ChatMessage. Ordered by SentAt on read, not stored ordered.
        public virtual ICollection<ChatMessage>? ChatMessages { get; set; }
    }
}
