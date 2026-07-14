using System.Reflection;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;

namespace RemSolution.Application.Common.Behaviours;

/// <summary>
/// Enforces the per-agency feature toggles: a request marked
/// <c>[RequiresFeature(...)]</c> is refused with 403 when the current
/// tenant has a row disabling that feature. No row means enabled — rows
/// exist to switch modules off — so agencies work without seeding.
/// The toggle is per-agency, not per-user: a disabled feature blocks the
/// agency administrator too (unlike permissions, which the admin holds
/// implicitly). Requests without a tenant (platform admin, anonymous) pass
/// through: there is no agency whose toggles could apply, and the tenant
/// query filters already make tenant data unreachable for them.
/// </summary>
public class FeatureEnforcementBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantProvider _tenant;

    public FeatureEnforcementBehaviour(IApplicationDbContext context, ITenantProvider tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requiresFeature = request.GetType().GetCustomAttribute<RequiresFeatureAttribute>();

        if (requiresFeature is null || _tenant.AgencyId is null)
        {
            return await next();
        }

        // The tenant query filter scopes the lookup to the current agency.
        var disabled = await _context.AgencyFeatures
            .AnyAsync(f => f.Feature == requiresFeature.Feature && !f.Enabled, cancellationToken);

        if (disabled)
        {
            throw new ForbiddenAccessException();
        }

        return await next();
    }
}
