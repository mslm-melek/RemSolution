using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Domain.Constants;
using RemSolution.Application.Features.Dashboard.DTOs;
using RemSolution.Application.Features.Dashboard.Queries.GetDashboardQuery;

namespace RemSolution.Web.Endpoints;

public class Dashboard : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this)
            .RequireAuthorization();

        group.MapGet(GetDashboard, policy: Permissions.DashboardView);
    }

    public async Task<Ok<DashboardDto>> GetDashboard(
        ISender sender, [AsParameters] GetDashboardQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }
}
