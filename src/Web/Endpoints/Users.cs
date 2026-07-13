using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
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
    public async Task<Ok<CurrentUserDto>> GetCurrentUser(ClaimsPrincipal principal, UserManager<ApplicationUser> userManager)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return TypedResults.Ok(new CurrentUserDto(false, null, null));

        var user = await userManager.GetUserAsync(principal);

        return TypedResults.Ok(new CurrentUserDto(
            true,
            principal.Identity.Name,
            user?.FullName));
    }
}

public record CurrentUserDto(bool IsAuthenticated, string? UserName, string? FullName);
