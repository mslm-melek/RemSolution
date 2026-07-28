using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;

namespace RemSolution.Web.Middleware;

/// <summary>
/// Lets a platform administrator work inside a single agency as that agency —
/// every tenant-scoped screen, read AND write — by sending the target agency in
/// the <c>X-Impersonate-Agency</c> header. When the header is present and the
/// caller is a platform admin, the middleware pushes the agency as the
/// <see cref="AmbientTenant"/> (so the existing EF tenant query filters resolve
/// to that agency, and inserts are stamped with it — no query or handler changes)
/// and opens an <see cref="ImpersonationScope"/>, which satisfies the agency's
/// permission policies for the duration of the request.
///
/// It sits between <c>UseAuthentication</c> and <c>UseAuthorization</c> so the
/// ambient/flag are live when the endpoint's permission policy evaluates, and so
/// the <c>using</c> wraps the whole downstream pipeline including EF materialization
/// and response serialization.
///
/// What bounds it, now that writes are allowed: the role check below (an agency
/// user forging the header falls through to their own tenant), one agency per
/// request, an AuditLog row per request naming the acting user and the verb, and
/// the agency's own rules still applying underneath — a lapsed subscription or a
/// disabled feature refuses the write here exactly as it would for the agency's
/// own administrator, because the platform admin can lift either one properly
/// instead of writing around it.
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

        // Agency is the tenant root (not ITenantEntity), so this lookup is unfiltered.
        if (!await db.Agencies.AnyAsync(a => a.Id == agencyId, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // Recorded before the request runs, so an attempted write that then fails
        // is on record too.
        await auditor.RecordAsync(
            agencyId, context.Request.Method, context.Request.Path, context.RequestAborted);

        using (AmbientTenant.Push(agencyId))
        using (ImpersonationScope.Begin())
        {
            await _next(context);
        }
    }
}
