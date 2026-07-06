using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Common;
using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RemSolution.Infrastructure.Data.Interceptors;

/// <summary>
/// An agency without an Active, in-period subscription can read its data but
/// not change it: any SaveChanges touching an ITenantEntity while a tenant is
/// present requires an active subscription, otherwise
/// <see cref="SubscriptionRequiredException"/> (402). Registered after
/// TenantEntityInterceptor so tenant violations (403) win over billing (402).
/// Without a tenant (seeding, platform admin) writes pass through untouched.
/// </summary>
public class SubscriptionEnforcementInterceptor : SaveChangesInterceptor
{
    private readonly ITenantProvider _tenant;
    private readonly TimeProvider _dateTime;

    public SubscriptionEnforcementInterceptor(ITenantProvider tenant, TimeProvider dateTime)
    {
        _tenant = tenant;
        _dateTime = dateTime;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (RequiresActiveSubscription(eventData.Context, out var context, out var agencyId)
            && !ActiveSubscriptions(context, agencyId).Any())
        {
            throw new SubscriptionRequiredException();
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (RequiresActiveSubscription(eventData.Context, out var context, out var agencyId)
            && !await ActiveSubscriptions(context, agencyId).AnyAsync(cancellationToken))
        {
            throw new SubscriptionRequiredException();
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private bool RequiresActiveSubscription(DbContext? eventContext, out DbContext context, out int agencyId)
    {
        context = null!;
        agencyId = default;

        if (eventContext is null || _tenant.AgencyId is not int tenantAgencyId)
        {
            return false;
        }

        context = eventContext;
        agencyId = tenantAgencyId;

        return context.ChangeTracker.Entries<ITenantEntity>().Any(e =>
            e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
    }

    private IQueryable<AgencySubscription> ActiveSubscriptions(DbContext context, int agencyId)
        => context.Set<AgencySubscription>()
            .Where(AgencySubscription.IsActiveFor(agencyId, _dateTime.GetUtcNow()));
}
