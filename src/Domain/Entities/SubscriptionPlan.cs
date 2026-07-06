namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// Platform-level plan catalog (not tenant data): defines the quota an
    /// agency buys. Limits are enforced at creation time in the Car/Client
    /// create handlers.
    /// </summary>
    public class SubscriptionPlan : BaseAuditableEntity
    {
        public string Name { get; set; } = string.Empty;
        public int MaxCars { get; set; }
        public int MaxClients { get; set; }
        public decimal Price { get; set; }
        public virtual ICollection<AgencySubscription>? Subscriptions { get; set; }
    }
}
