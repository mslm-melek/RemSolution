using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;

namespace RemSolution.Web.Middleware;

/// <summary>
/// Lets a platform administrator browse a single agency's tenant-scoped data
/// (Cars, Clients) read-only, by sending the target agency in the
/// <c>X-Impersonate-Agency</c> header. When the header is present and the caller
/// is a platform admin on a GET request, the middleware pushes the agency as the
/// <see cref="AmbientTenant"/> (so the existing EF tenant query filters resolve
/// to that agency — no query changes) and opens an <see cref="ImpersonationScope"/>
/// (which unlocks the read-only permission bypass in the per-permission policy).
///
/// It sits between <c>UseAuthentication</c> and <c>UseAuthorization</c> so the
/// ambient/flag are live when the endpoint's permission policy evaluates, and so
/// the <c>using</c> wraps the whole downstream pipeline including EF materialization
/// and response serialization. Read-only is enforced twice: only GET may impersonate
/// (any other verb with the header is refused), and the policy bypass lists only the
/// read permissions.
/// </summary>
public class PlatformAdminImpersonationMiddleware
{
    public const string HeaderName = "X-Impersonate-Agency";

    private readonly RequestDelegate _next;

    public PlatformAdminImpersonationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IApplicationDbContext db,
        IImpersonationAuditor auditor)
    {
        // No header, not a platform admin, or unparseable value → ordinary pipeline.
        // An agency user forging the header falls through here (role check fails),
        // so they still see only their own tenant.
        if (!context.Request.Headers.TryGetValue(HeaderName, out var raw) ||
            !context.User.IsInRole(Roles.PlatformAdministrator) ||
            !int.TryParse(raw, out var agencyId))
        {
            await _next(context);
            return;
        }

        // Read-only: only GET may impersonate, regardless of the policy bypass.
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        // Agency is the tenant root (not ITenantEntity), so this lookup is unfiltered.
        if (!await db.Agencies.AnyAsync(a => a.Id == agencyId, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await auditor.RecordAsync(agencyId, context.Request.Path, context.RequestAborted);

        using (AmbientTenant.Push(agencyId))
        using (ImpersonationScope.Begin())
        {
            await _next(context);
        }
    }
}
