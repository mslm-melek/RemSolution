namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// Per-agency override for one feature module (see <c>FeatureFlags</c>).
    /// No row means the feature follows the active subscription's plan; a row
    /// forces it on or off regardless of the plan. See
    /// <c>AgencyFeatureResolver</c>, which owns that precedence.
    /// </summary>
    public class AgencyFeature : BaseAuditableEntity, ITenantEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        public string Feature { get; set; } = string.Empty;
        public bool Enabled { get; set; }
    }
}
