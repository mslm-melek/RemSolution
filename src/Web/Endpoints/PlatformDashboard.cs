using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Features.PlatformDashboard.DTOs;
using RemSolution.Application.Features.PlatformDashboard.Queries.GetPlatformDashboardQuery;
using RemSolution.Domain.Constants;

namespace RemSolution.Web.Endpoints;

// The app owner's cross-agency overview. Separate from the (tenant-scoped)
// Dashboard group: same screen name, different audience and a different
// authorization rule.
public class PlatformDashboard : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .RequireAuthorization(Policies.PlatformAdminOnly)
            .MapGet(GetPlatformDashboard);
    }

    public async Task<Ok<PlatformDashboardDto>> GetPlatformDashboard(
        ISender sender, [AsParameters] GetPlatformDashboardQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }
}
