namespace RemSolution.Domain.Entities
{
    public class ExtraServicesType : BaseAuditableEntity
    {
        public string? Name { get; set; }
        public decimal? Amount { get; set; }
        // Deactivation, not deletion: an inactive type is hidden from new-entry
        // pickers but kept so historical extra services still resolve their type.
        public bool IsActive { get; set; } = true;
        public virtual ICollection<ExtraService> ExtraServices { get; set; } = new List<ExtraService>();

    }
}
