using System.Reflection;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Features;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.Common.Behaviours;

/// <summary>
/// Enforces the per-agency feature entitlement: a request marked
/// <c>[RequiresFeature(...)]</c> is refused with 403 unless the feature is in
/// the agency's effective set (allow-list — its active plan's features, adjusted
/// by per-agency override rows; see <see cref="AgencyFeatureResolver"/>). The
/// gate is per-agency, not per-user: a disabled feature blocks the agency
/// administrator too. Requests without a tenant (platform admin, anonymous) pass
/// through — there is no agency whose entitlement could apply, and the tenant
/// query filters already make tenant data unreachable for them.
/// <para>
/// This gate answers the entitlement question only. A LAPSED subscription is a
/// write freeze rather than a lockout and is enforced elsewhere — see the note
/// on the refusal path below.
/// </para>
/// </summary>
public class FeatureEnforcementBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantProvider _tenant;
    private readonly TimeProvider _dateTime;

    public FeatureEnforcementBehaviour(IApplicationDbContext context, ITenantProvider tenant, TimeProvider dateTime)
    {
        _context = context;
        _tenant = tenant;
        _dateTime = dateTime;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requiresFeature = request.GetType().GetCustomAttribute<RequiresFeatureAttribute>();

        if (requiresFeature is null || _tenant.AgencyId is not int agencyId)
        {
            return await next();
        }

        var now = _dateTime.GetUtcNow();

        var enabled = await AgencyFeatureResolver.GetEnabledFeaturesAsync(
            _context, agencyId, now, cancellationToken);

        if (enabled.Contains(requiresFeature.Feature))
        {
            return await next();
        }

        // Two very different situations reach this line, and they are not the
        // same refusal.
        //
        // A plan that simply excludes the module is an entitlement decision: 403,
        // for reads as much as writes.
        //
        // A LAPSED subscription is not. Its effective feature set is empty, so
        // this gate would otherwise lock the agency out of its own client files
        // and bookings entirely — including the invoice it has to look at to pay
        // us. So the request passes, and SubscriptionEnforcementInterceptor stops
        // the WRITES (as does SubscriptionGuard in the create handlers, with the
        // actionable SubscriptionRequired). Lapsing freezes an agency; it does
        // not take its records away.
        //
        // The extra query is on the refusal path only, which is not a hot path.
        var subscribed = await _context.AgencySubscriptions
            .AnyAsync(AgencySubscription.IsActiveFor(agencyId, now), cancellationToken);

        if (!subscribed)
        {
            return await next();
        }

        throw new ForbiddenAccessException();
    }
}
