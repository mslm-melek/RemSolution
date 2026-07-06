namespace RemSolution.Domain.Entities
{
    public class ExtraService : BaseAuditableEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        public int? RentingId { get; set; }
        public virtual Renting? Renting { get; set; }
        public virtual int? ExtraServicesTypeId { get; set; }
        public virtual ExtraServicesType? ExtraServicesType { get; set; }
        public decimal? TotalAmount { get; set; }
    }
}
