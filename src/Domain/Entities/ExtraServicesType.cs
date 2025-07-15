namespace RemSolution.Domain.Entities
{
    public class ExtraServicesType : BaseAuditableEntity
    {
        public string? Name { get; set; }
        public decimal? Amount { get; set; }
        public virtual ICollection<ExtraService> ExtraServices { get; set; } = new List<ExtraService>();

    }
}
