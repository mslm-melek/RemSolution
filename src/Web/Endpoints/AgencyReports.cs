using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Features.AgencyReport.Commands.ResolveAgencyReportCommand;
using RemSolution.Application.Features.AgencyReport.DTOs;
using RemSolution.Application.Features.AgencyReport.Queries.GetAgencyReportsQuery;
using RemSolution.Domain.Constants;

namespace RemSolution.Web.Endpoints;

// The platform's arbitration queue. An agency reads the complaints against
// itself through Agencies/me/reports, and a customer raises one through the
// Marketplace group — this half belongs to the app owner alone.
public class AgencyReports : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .RequireAuthorization(Policies.PlatformAdminOnly)
            .MapGet(GetAgencyReports)
            .MapPost(ResolveAgencyReport, "{id}/resolve");
    }

    public async Task<Ok<PaginatedList<AgencyReportDto>>> GetAgencyReports(
        ISender sender, [AsParameters] GetAgencyReportsQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Results<NoContent, BadRequest>> ResolveAgencyReport(
        ISender sender, int id, ResolveAgencyReportCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }
}
