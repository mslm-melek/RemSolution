namespace RemSolution.Domain.Entities
{
    // Immutable snapshot of a renting's period, written by the renting-completion
    // event handler. Tenant-scoped for defense in depth (the parent renting is
    // tenant-scoped too).
    public class RentingHistory : BaseAuditableEntity, ITenantEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? StartMileage { get; set; }
        public int? EndMileage { get; set; }
        public Money? Price { get; set; }
        public int? RentingId { get; set; }
        public virtual Renting? Renting { get; set; }
        public RentingState RentingState { get; set; }

    }
}
