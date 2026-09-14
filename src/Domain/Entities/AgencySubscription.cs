using System.Linq.Expressions;

namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// An agency's subscription to a plan for a period. Platform-level billing
    /// data managed by the platform admin — deliberately NOT ITenantEntity:
    /// tenant query filters would hide the rows from the (tenant-less) platform
    /// admin, and agencies only ever see their own row through queries that
    /// filter on the current tenant explicitly.
    /// </summary>
    public class AgencySubscription : BaseAuditableEntity
    {
        public int AgencyId { get; set; }
        public virtual Agency? Agency { get; set; }
        public int PlanId { get; set; }
        public virtual SubscriptionPlan? Plan { get; set; }
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset EndDate { get; set; }
        public SubscriptionStatus Status { get; set; }

        /// <summary>
        /// The single definition of "this agency may write": status is Active
        /// AND the period covers <paramref name="now"/>. A lapsed EndDate blocks
        /// writes even if the admin has not flipped the status yet.
        /// </summary>
        public static Expression<Func<AgencySubscription, bool>> IsActiveFor(int agencyId, DateTimeOffset now) =>
            s => s.AgencyId == agencyId
                 && s.Status == SubscriptionStatus.Active
                 && s.StartDate <= now
                 && now < s.EndDate;

        /// <summary>
        /// The same three terms without the agency, for the marketplace: there the
        /// agency is the correlated column of an enclosing query, which cannot be
        /// baked into <see cref="IsActiveFor"/>'s parameter. The two must say the
        /// same thing — change one, change both.
        /// </summary>
        public static Expression<Func<AgencySubscription, bool>> IsActiveAt(DateTimeOffset now) =>
            s => s.Status == SubscriptionStatus.Active
                 && s.StartDate <= now
                 && now < s.EndDate;
    }
}
