using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using RemSolution.Web.Infrastructure;
using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Features;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Application.Features.Users.Commands.CreateAgencyUserCommand;
using RemSolution.Application.Features.Users.Commands.CreateAgencyUserByAdminCommand;
using RemSolution.Application.Features.Users.Commands.UpdateAgencyUserCommand;
using RemSolution.Application.Features.Users.Commands.SetAgencyUserActiveCommand;
using RemSolution.Application.Features.Users.Commands.ResetAgencyUserPasswordCommand;
using RemSolution.Application.Features.Users.Commands.UpdateMyAgencyUserCommand;
using RemSolution.Application.Features.Users.Commands.SetMyAgencyUserActiveCommand;
using RemSolution.Application.Features.Users.Commands.UpdateMyProfileCommand;
using RemSolution.Application.Features.Users.Commands.ChangeMyPasswordCommand;
using RemSolution.Application.Features.Users.Commands.UpdateMyLanguageCommand;
using RemSolution.Application.Features.Users.Commands.UpdateMyHomeWidgetsCommand;
using RemSolution.Application.Features.Users.Commands.UpdateMyHomeActionsCommand;
using RemSolution.Application.Features.Users.Queries.GetMyProfileQuery;
using RemSolution.Application.Features.Users.Queries.GetAgencyUsersQuery;
using RemSolution.Application.Features.Users.Queries.GetAgencyUserByIdQuery;
using RemSolution.Application.Features.Users.Queries.GetMyAgencyUsersQuery;
using RemSolution.Application.Features.Users.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Infrastructure.Identity;

namespace RemSolution.Web.Endpoints;

public class Users : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        // "me" is anonymous on purpose: the SPA calls it on startup to decide
        // which navigation to render, so it must never trigger an auth
        // challenge. Staff creation is an agency-administrator operation.
        var group = app.MapGroup(this)
            .MapGet(GetCurrentUser, "me")
            .MapPost(CreateAgencyUser, policy: Policies.AgencyAdminOnly)
            .MapPost(CreateAgencyUserByAdmin, "by-admin", Policies.PlatformAdminOnly)
            .MapGet(GetAgencyUsers, "by-agency/{agencyId}", Policies.PlatformAdminOnly)
            .MapGet(GetAgencyUserById, "{id}", Policies.PlatformAdminOnly)
            .MapPut(UpdateAgencyUser, "{id}", Policies.PlatformAdminOnly)
            .MapPut(SetAgencyUserActive, "{id}/active", Policies.PlatformAdminOnly)
            .MapPut(ResetAgencyUserPassword, "{id}/password", Policies.PlatformAdminOnly)
            // Agency-admin self-service over their own agency's staff.
            .MapGet(GetMyAgencyUsers, "my-agency", Policies.AgencyAdminOnly)
            .MapPut(UpdateMyAgencyUser, "my-agency/{id}/permissions", Policies.AgencyAdminOnly)
            .MapPut(SetMyAgencyUserActive, "my-agency/{id}/active", Policies.AgencyAdminOnly);

        // Self-service profile: any authenticated user manages their own account
        // (unlike the anonymous "me" probe, these require a signed-in user).
        group.MapGet("me/profile", GetMyProfile).WithName(nameof(GetMyProfile)).RequireAuthorization();
        group.MapPut("me/profile", UpdateMyProfile).WithName(nameof(UpdateMyProfile)).RequireAuthorization();
        group.MapPut("me/password", ChangeMyPassword).WithName(nameof(ChangeMyPassword)).RequireAuthorization();
        group.MapPut("me/language", UpdateMyLanguage).WithName(nameof(UpdateMyLanguage)).RequireAuthorization();
        group.MapPut("me/home-widgets", UpdateMyHomeWidgets).WithName(nameof(UpdateMyHomeWidgets)).RequireAuthorization();
        group.MapPut("me/home-actions", UpdateMyHomeActions).WithName(nameof(UpdateMyHomeActions)).RequireAuthorization();
    }

    public async Task<Ok<MyProfileDto>> GetMyProfile(ISender sender)
    {
        var result = await sender.Send(new GetMyProfileQuery());
        return TypedResults.Ok(result);
    }

    public async Task<NoContent> UpdateMyProfile(ISender sender, UpdateMyProfileCommand command)
    {
        await sender.Send(command);
        return TypedResults.NoContent();
    }

    public async Task<NoContent> ChangeMyPassword(ISender sender, ChangeMyPasswordCommand command)
    {
        await sender.Send(command);
        return TypedResults.NoContent();
    }

    // Stores the choice AND writes the culture cookie in the same response. The
    // stored value is what follows the account across devices (via the
    // PreferredLanguage claim); the cookie is what the server-rendered Identity
    // pages read, and it also covers the window before the auth ticket is
    // re-minted with the new claim.
    public async Task<NoContent> UpdateMyLanguage(
        ISender sender, HttpContext httpContext, UpdateMyLanguageCommand command)
    {
        await sender.Send(command);

        CultureCookie.Write(httpContext.Response, command.Language);

        return TypedResults.NoContent();
    }

    // The tiles the caller pinned to their own home screen, in their order. The
    // choice rides back on the current-user probe (below), so the home screen
    // renders from the one call the SPA already makes at startup.
    public async Task<NoContent> UpdateMyHomeWidgets(ISender sender, UpdateMyHomeWidgetsCommand command)
    {
        await sender.Send(command);
        return TypedResults.NoContent();
    }

    // The quick actions the caller keeps on their landing screen, in their order.
    // Rides back on the current-user probe like the tiles do.
    public async Task<NoContent> UpdateMyHomeActions(ISender sender, UpdateMyHomeActionsCommand command)
    {
        await sender.Send(command);
        return TypedResults.NoContent();
    }

    public async Task<Ok<IReadOnlyList<AgencyUserDto>>> GetMyAgencyUsers(ISender sender)
    {
        var result = await sender.Send(new GetMyAgencyUsersQuery());
        return TypedResults.Ok(result);
    }

    public async Task<Results<NoContent, BadRequest>> UpdateMyAgencyUser(ISender sender, string id, UpdateMyAgencyUserCommand command)
    {
        if (id != command.UserId)
            return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }

    public async Task<Results<NoContent, BadRequest>> SetMyAgencyUserActive(ISender sender, string id, SetMyAgencyUserActiveCommand command)
    {
        if (id != command.UserId)
            return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }

    public async Task<Created<string>> CreateAgencyUser(ISender sender, CreateAgencyUserCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/users/{id}", id);
    }

    public async Task<Created<string>> CreateAgencyUserByAdmin(ISender sender, CreateAgencyUserByAdminCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/users/{id}", id);
    }

    public async Task<Ok<IReadOnlyList<AgencyUserDto>>> GetAgencyUsers(ISender sender, int agencyId)
    {
        var result = await sender.Send(new GetAgencyUsersQuery(agencyId));
        return TypedResults.Ok(result);
    }

    public async Task<Results<Ok<AgencyUserDto>, NotFound>> GetAgencyUserById(ISender sender, string id)
    {
        var result = await sender.Send(new GetAgencyUserByIdQuery(id));

        if (result is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(result);
    }

    public async Task<Results<NoContent, BadRequest>> UpdateAgencyUser(ISender sender, string id, UpdateAgencyUserCommand command)
    {
        if (id != command.UserId)
            return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }

    public async Task<Results<NoContent, BadRequest>> SetAgencyUserActive(ISender sender, string id, SetAgencyUserActiveCommand command)
    {
        if (id != command.UserId)
            return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }

    public async Task<Results<NoContent, BadRequest>> ResetAgencyUserPassword(ISender sender, string id, ResetAgencyUserPasswordCommand command)
    {
        if (id != command.UserId)
            return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
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
        ITenantProvider tenant,
        TimeProvider dateTime)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return TypedResults.Ok(new CurrentUserDto(
                false, null, null, null, null, null, Array.Empty<string>(), Array.Empty<string>(), false, false, null, null));

        var user = await userManager.GetUserAsync(principal);

        // The SPA branches its navigation on the role: a platform administrator
        // gets the agency-grouped console, agency users the flat module list.
        var role =
            principal.IsInRole(Roles.PlatformAdministrator) ? Roles.PlatformAdministrator :
            principal.IsInRole(Roles.AgencyAdministrator) ? Roles.AgencyAdministrator :
            principal.IsInRole(Roles.AgencyStaff) ? Roles.AgencyStaff :
            principal.IsInRole(Roles.Customer) ? Roles.Customer : null;

        // A platform admin inside an agency workspace satisfies every permission
        // policy for the duration of the request (see the impersonation
        // middleware), so the SPA is handed that same set — otherwise the
        // feature-driven navigation would hide screens the API would allow.
        var impersonating = principal.IsInRole(Roles.PlatformAdministrator) && ImpersonationScope.IsActive;

        var granted = principal.IsInRole(Roles.AgencyAdministrator) || impersonating
            ? Permissions.All
            : principal.FindAll(Claims.Permission).Select(c => c.Value).ToArray();

        var permissions = granted;
        var features = Array.Empty<string>();
        string? agencyName = null;

        if (tenant.AgencyId is int agencyId)
        {
            // Effective features = active plan + per-agency overrides; a
            // permission only counts while its feature is enabled.
            var enabled = await AgencyFeatureResolver.GetEnabledFeaturesAsync(
                context, agencyId, dateTime.GetUtcNow(), CancellationToken.None);

            features = enabled.ToArray();
            permissions = FeatureCatalog.EffectivePermissions(granted, enabled).ToArray();

            agencyName = await context.Agencies
                .AsNoTracking()
                .Where(a => a.Id == agencyId)
                .Select(a => a.Name)
                .FirstOrDefaultAsync();
        }

        return TypedResults.Ok(new CurrentUserDto(
            true,
            principal.Identity.Name,
            user?.FullName,
            role,
            tenant.AgencyId,
            agencyName,
            permissions,
            features,
            impersonating,
            // Read from the user row rather than the claim: this endpoint is
            // what the SPA polls to decide where to send someone, and it must
            // agree with the middleware that will accept or refuse their next
            // call — which also reads the row.
            user?.MustChangePassword == true,
            // Null when the user has never chosen, which is what tells the home
            // screen to show its default tiles instead of none.
            HomeWidgets.Parse(user?.HomeWidgets),
            HomeActions.Parse(user?.HomeActions)));
    }
}

public record CurrentUserDto(
    bool IsAuthenticated,
    string? UserName,
    string? FullName,
    string? Role,
    int? AgencyId,
    string? AgencyName,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<string> Features,
    // True when the caller is a platform administrator working inside the agency
    // named above rather than their own context. AgencyId/AgencyName/Permissions/
    // Features then all describe that agency — the SPA shows the agency's
    // navigation and a banner saying whose data is on screen.
    bool IsImpersonating,
    // The account is still on the temporary password it was provisioned with.
    // Every other API call will be refused until it is replaced, so the SPA
    // routes straight to the change-password screen rather than rendering an
    // app whose every request 403s.
    bool MustChangePassword,
    // The home-screen shortcut tiles this user pinned, in their chosen order
    // (Domain.Constants.HomeWidgets keys). Null means they never chose, and the
    // home screen falls back to its default set — an empty list is the
    // deliberate "no tiles", so the two must stay distinguishable.
    IReadOnlyList<string>? HomeWidgets,
    // The quick actions this user keeps on their landing screen, in their chosen
    // order (Domain.Constants.HomeActions keys). Null / empty read exactly as
    // HomeWidgets above: never chose ⇒ defaults, empty ⇒ deliberately none.
    IReadOnlyList<string>? HomeActions);
