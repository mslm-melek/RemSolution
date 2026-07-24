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
        public Money? Price { get; set; }
        public RentingState RentingState { get; set; }
        public virtual ICollection<ExtraService>? ExtraServices { get; set; }
        public virtual ICollection<RentingHistory>? RentingHistories { get; set; }
        public virtual ICollection<Reservation>? Reservations { get; set; }
    }
}
