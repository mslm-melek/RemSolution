namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// A feature (see <c>FeatureFlags</c>) included in a subscription plan.
    /// Platform-level, not tenant data: it defines what an agency's plan unlocks.
    /// An agency's effective features = its active plan's features, adjusted by
    /// per-agency <see cref="AgencyFeature"/> overrides.
    /// </summary>
    public class PlanFeature : BaseAuditableEntity
    {
        public int PlanId { get; set; }
        public virtual SubscriptionPlan? Plan { get; set; }
        public string Feature { get; set; } = string.Empty;
    }
}
