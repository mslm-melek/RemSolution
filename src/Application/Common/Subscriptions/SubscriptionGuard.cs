using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.Common.Subscriptions;

public static class SubscriptionGuard
{
    /// <summary>
    /// Quota check shared by the Car/Client create handlers. Must run inside a
    /// transaction holding the tenant write lock (see
    /// <see cref="IApplicationDbContext.AcquireTenantWriteLockAsync"/>) so the
    /// count cannot race a concurrent insert. <paramref name="tenantScopedSet"/>
    /// is counted as-is — tenant query filters already scope it to the caller.
    /// Throws <see cref="SubscriptionRequiredException"/> without an active
    /// subscription and <see cref="PlanLimitExceededException"/> at quota.
    /// No-op without a tenant (seeding, platform admin).
    /// </summary>
    public static async Task EnsureWithinPlanLimitAsync<TEntity>(
        IApplicationDbContext context,
        ITenantProvider tenant,
        TimeProvider dateTime,
        IQueryable<TEntity> tenantScopedSet,
        Func<SubscriptionPlan, int> limitSelector,
        string resource,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        if (tenant.AgencyId is not int agencyId)
        {
            return;
        }

        var plan = await context.AgencySubscriptions
            .Where(AgencySubscription.IsActiveFor(agencyId, dateTime.GetUtcNow()))
            .Select(s => s.Plan)
            .FirstOrDefaultAsync(cancellationToken);

        if (plan is null)
        {
            throw new SubscriptionRequiredException();
        }

        var limit = limitSelector(plan);
        var count = await tenantScopedSet.CountAsync(cancellationToken);

        if (count >= limit)
        {
            throw new PlanLimitExceededException(resource, limit);
        }
    }
}
