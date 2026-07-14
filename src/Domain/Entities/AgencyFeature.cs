namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// Per-agency toggle for one feature module (see Constants.Features).
    /// No row for a feature means the feature is enabled; rows switch a
    /// module off or explicitly back on.
    /// </summary>
    public class AgencyFeature : BaseAuditableEntity, ITenantEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        public string Feature { get; set; } = string.Empty;
        public bool Enabled { get; set; }
    }
}
