using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Features.Agency.Commands.CreateAgencyCommand;
using RemSolution.Application.Features.Agency.Commands.DeleteAgencyCommand;
using RemSolution.Application.Features.Agency.Commands.SetAgencyFeatureCommand;
using RemSolution.Application.Features.Agency.Commands.UpdateAgencyCommand;
using RemSolution.Application.Features.Agency.DTOs;
using RemSolution.Application.Features.Agency.Queries.GetAgenciesQuery;
using RemSolution.Application.Features.Agency.Queries.GetAgencyByIdQuery;
using RemSolution.Application.Features.Agency.Queries.GetAgencyFeaturesQuery;
using RemSolution.Domain.Constants;

namespace RemSolution.Web.Endpoints;

public class Agencies : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .RequireAuthorization(Policies.PlatformAdminOnly)
            .MapGet(GetAgencies)
            .MapGet(GetAgencyById, "{id}")
            .MapPost(CreateAgency)
            .MapPut(UpdateAgency, "{id}")
            .MapDelete(DeleteAgency, "{id}")
            .MapGet(GetAgencyFeatures, "{id}/features")
            .MapPut(SetAgencyFeature, "{id}/features");
    }

    public async Task<Ok<IReadOnlyList<AgencyFeatureDto>>> GetAgencyFeatures(ISender sender, int id)
    {
        var result = await sender.Send(new GetAgencyFeaturesQuery(id));
        return TypedResults.Ok(result);
    }

    public async Task<Results<NoContent, BadRequest>> SetAgencyFeature(ISender sender, int id, SetAgencyFeatureCommand command)
    {
        if (id != command.AgencyId)
            return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }

    public async Task<Ok<IList<AgencyDto>>> GetAgencies(ISender sender, [AsParameters] GetAgenciesQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Results<Ok<AgencyDto>, NotFound>> GetAgencyById(ISender sender, int id)
    {
        var result = await sender.Send(new GetAgencyByIdQuery(id));

        if (result is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(result);
    }

    public async Task<Created<int>> CreateAgency(ISender sender, CreateAgencyCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/Agencies/{id}", id);
    }

    public async Task<Results<NoContent, BadRequest>> UpdateAgency(ISender sender, int id, UpdateAgencyCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }

    public async Task<NoContent> DeleteAgency(ISender sender, int id)
    {
        await sender.Send(new DeleteAgencyCommand(id));
        return TypedResults.NoContent();
    }
}
