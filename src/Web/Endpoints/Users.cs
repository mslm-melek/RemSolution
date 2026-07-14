using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Constants;
using RemSolution.Infrastructure.Identity;

namespace RemSolution.Web.Endpoints;

public class Users : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        // Anonymous on purpose: the SPA calls this on startup to decide which
        // navigation to render, so it must never trigger an auth challenge.
        app.MapGroup(this)
            .MapGet(GetCurrentUser, "me");
    }

    // FullName is read from the store rather than a claim so a profile edit
    // shows up on the next page load without waiting for a cookie refresh.
    // Permissions come from the cookie claims — the same source the
    // permission policies check, so the SPA never shows a module the API
    // would refuse. Features are read live from AgencyFeatures (tenant
    // query filter scopes the lookup): a toggle applies on the next page
    // load, no re-login needed.
    public async Task<Ok<CurrentUserDto>> GetCurrentUser(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        IApplicationDbContext context,
        ITenantProvider tenant)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return TypedResults.Ok(new CurrentUserDto(false, null, null, Array.Empty<string>(), Array.Empty<string>()));

        var user = await userManager.GetUserAsync(principal);

        var permissions = principal.IsInRole(Roles.AgencyAdministrator)
            ? Permissions.All
            : principal.FindAll(Claims.Permission).Select(c => c.Value).ToArray();

        var features = Array.Empty<string>();

        if (tenant.AgencyId is not null)
        {
            var disabled = await context.AgencyFeatures
                .AsNoTracking()
                .Where(f => !f.Enabled)
                .Select(f => f.Feature)
                .ToListAsync();

            features = FeatureFlags.All.Except(disabled).ToArray();
        }

        return TypedResults.Ok(new CurrentUserDto(
            true,
            principal.Identity.Name,
            user?.FullName,
            permissions,
            features));
    }
}

public record CurrentUserDto(
    bool IsAuthenticated,
    string? UserName,
    string? FullName,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<string> Features);
