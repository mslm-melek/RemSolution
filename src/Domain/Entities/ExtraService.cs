namespace RemSolution.Domain.Entities
{
    public class ExtraService : BaseAuditableEntity
    {
        public int? RentingId { get; set; }
        public virtual Renting? Renting { get; set; }
        public virtual int? ExtraServicesTypeId { get; set; }
        public virtual ExtraServicesType? ExtraServicesType { get; set; }
        public decimal? TotalAmount { get; set; }
    }
}
