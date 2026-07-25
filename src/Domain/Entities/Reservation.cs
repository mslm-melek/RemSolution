namespace RemSolution.Domain.Entities
{
    public class Reservation : BaseAuditableEntity, ITenantEntity, IHasRowVersion
    {
        // Optimistic-concurrency token; see IHasRowVersion.
        public byte[]? RowVersion { get; set; }
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        // The car being held. Nullable and SetNull on car delete, mirroring
        // Renting — a reservation anchors availability on a specific car.
        public int? CarId { get; set; }
        public virtual Car? Car { get; set; }
        public int? ClientId { get; set; }
        public virtual Client? Client { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        // Snapshot price for the held period (from IPricingService), plus any
        // deposit already collected.
        public Money? Price { get; set; }
        public Money? PayedPrice { get; set; }
        public string? Notes { get; set; }
        // Lifecycle of the hold. A Pending hold expires at ExpiresAt unless
        // confirmed; confirming creates the Renting linked below.
        public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
        // When a Pending hold lapses. Set from AgencySettings.ReservationExpiryHours
        // at creation; the reservation-expiry job sweeps holds past this instant.
        public DateTime? ExpiresAt { get; set; }
        // Set when the hold is confirmed into an actual renting.
        public int? RentingId { get; set; }
        public virtual Renting? Renting { get; set; }
    }
}
