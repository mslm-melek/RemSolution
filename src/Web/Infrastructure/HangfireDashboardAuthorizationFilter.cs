using Hangfire.Dashboard;
using RemSolution.Domain.Constants;

namespace RemSolution.Web.Infrastructure;

// Locks the Hangfire dashboard to platform administrators. The dashboard sits
// behind the app's authentication middleware, so the principal is populated;
// this filter additionally requires the PlatformAdministrator role — the job
// infrastructure is cross-tenant and platform-operational, never agency-facing.
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var user = context.GetHttpContext().User;

        return user.Identity?.IsAuthenticated == true
            && user.IsInRole(Roles.PlatformAdministrator);
    }
}
