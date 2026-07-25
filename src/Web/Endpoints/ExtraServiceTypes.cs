using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Domain.Constants;
using RemSolution.Application.Features.ExtraServicesType.Commands.CreateExtraServicesTypeCommand;
using RemSolution.Application.Features.ExtraServicesType.Commands.DeactivateExtraServicesTypeCommand;
using RemSolution.Application.Features.ExtraServicesType.Commands.UpdateExtraServicesTypeCommand;
using RemSolution.Application.Features.ExtraServicesType.DTOs;
using RemSolution.Application.Features.ExtraServicesType.Queries.GetExtraServicesTypesQuery;

namespace RemSolution.Web.Endpoints;

public class ExtraServiceTypes : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this)
            .RequireAuthorization();

        group
            // Read is open to any authenticated user (staff pick a type); managing
            // the catalog is restricted to agency/platform administrators.
            .MapGet(GetExtraServiceTypes)
            .MapPost(CreateExtraServiceType, policy: Policies.AgencyOrPlatformAdmin)
            .MapPut(UpdateExtraServiceType, "{id}", Policies.AgencyOrPlatformAdmin)
            .MapDelete(DeactivateExtraServiceType, "{id}", Policies.AgencyOrPlatformAdmin);
    }

    public async Task<Ok<IList<ExtraServicesTypeDto>>> GetExtraServiceTypes(
        ISender sender, [AsParameters] GetExtraServicesTypesQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Created<int>> CreateExtraServiceType(
        ISender sender, CreateExtraServicesTypeCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/extraservicetypes/{id}", id);
    }

    public async Task<Results<NoContent, BadRequest>> UpdateExtraServiceType(
        ISender sender, int id, UpdateExtraServicesTypeCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);
        return TypedResults.NoContent();
    }

    public async Task<NoContent> DeactivateExtraServiceType(ISender sender, int id)
    {
        await sender.Send(new DeactivateExtraServicesTypeCommand(id));
        return TypedResults.NoContent();
    }
}
