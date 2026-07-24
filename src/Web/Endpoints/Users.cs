using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Features;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Users.Commands.CreateAgencyUserCommand;
using RemSolution.Application.Features.Users.Commands.CreateAgencyUserByAdminCommand;
using RemSolution.Application.Features.Users.Commands.UpdateAgencyUserCommand;
using RemSolution.Application.Features.Users.Commands.SetAgencyUserActiveCommand;
using RemSolution.Application.Features.Users.Commands.ResetAgencyUserPasswordCommand;
using RemSolution.Application.Features.Users.Commands.UpdateMyAgencyUserCommand;
using RemSolution.Application.Features.Users.Commands.SetMyAgencyUserActiveCommand;
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
        app.MapGroup(this)
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
            return TypedResults.Ok(new CurrentUserDto(false, null, null, null, null, null, Array.Empty<string>(), Array.Empty<string>()));

        var user = await userManager.GetUserAsync(principal);

        // The SPA branches its navigation on the role: a platform administrator
        // gets the agency-grouped console, agency users the flat module list.
        var role =
            principal.IsInRole(Roles.PlatformAdministrator) ? Roles.PlatformAdministrator :
            principal.IsInRole(Roles.AgencyAdministrator) ? Roles.AgencyAdministrator :
            principal.IsInRole(Roles.AgencyStaff) ? Roles.AgencyStaff : null;

        var granted = principal.IsInRole(Roles.AgencyAdministrator)
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
            features));
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
    IReadOnlyCollection<string> Features);
