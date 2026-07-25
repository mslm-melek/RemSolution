namespace RemSolution.Domain.Entities
{
    // Global catalog of add-on types (GPS, child seat, ...) shared by all
    // agencies; managed by agency or platform administrators (not staff).
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
