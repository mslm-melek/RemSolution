using System.Reflection;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Features;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;

namespace RemSolution.Application.Common.Behaviours;

/// <summary>
/// Enforces the per-agency feature entitlement: a request marked
/// <c>[RequiresFeature(...)]</c> is refused with 403 unless the feature is in
/// the agency's effective set (allow-list — its active plan's features, adjusted
/// by per-agency override rows; see <see cref="AgencyFeatureResolver"/>). An
/// agency with no active subscription has no features, so every gated module is
/// refused. The gate is per-agency, not per-user: a disabled feature blocks the
/// agency administrator too. Requests without a tenant (platform admin,
/// anonymous) pass through — there is no agency whose entitlement could apply,
/// and the tenant query filters already make tenant data unreachable for them.
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

        var enabled = await AgencyFeatureResolver.GetEnabledFeaturesAsync(
            _context, agencyId, _dateTime.GetUtcNow(), cancellationToken);

        if (!enabled.Contains(requiresFeature.Feature))
        {
            throw new ForbiddenAccessException();
        }

        return await next();
    }
}
