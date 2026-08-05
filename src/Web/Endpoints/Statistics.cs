using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Features.Statistics.DTOs;
using RemSolution.Application.Features.Statistics.Queries.GetStatisticsQuery;
using RemSolution.Domain.Constants;

namespace RemSolution.Web.Endpoints;

public class Statistics : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this)
            .RequireAuthorization();

        group
            .MapGet(GetStatistics, policy: Permissions.DashboardView);
    }

    // Rentings and money per month or per year, for the fleet or for one car.
    // Gated on the dashboard's permission — see GetStatisticsQuery for why.
    public async Task<Ok<StatisticsDto>> GetStatistics(
        ISender sender, [AsParameters] GetStatisticsQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }
}
