namespace RemSolution.Domain.Entities
{
    public class Reservation : BaseAuditableEntity, ITenantEntity, IHasRowVersion
    {
        // Optimistic-concurrency token; see IHasRowVersion.
        public byte[]? RowVersion { get; set; }
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        public int? ClientId { get; set; }
        public virtual Client? Client { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public Money? Price { get; set; }
        public Money? PayedPrice { get; set; }
        public string? Notes { get; set; }
        public int? RentingId { get; set; }
        public virtual Renting? Renting { get; set; }
        public RentingState? RentingState { get; set; }

    }
}
