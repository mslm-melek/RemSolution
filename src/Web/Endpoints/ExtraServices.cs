using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Domain.Constants;
using RemSolution.Application.Features.ExtraService.Commands.CreateExtraServiceCommand;
using RemSolution.Application.Features.ExtraService.Commands.DeleteExtraServiceCommand;
using RemSolution.Application.Features.ExtraService.Commands.UpdateExtraServiceCommand;
using RemSolution.Application.Features.ExtraService.DTOs;
using RemSolution.Application.Features.ExtraService.Queries.GetExtraServicesByRentingQuery;

namespace RemSolution.Web.Endpoints;

public class ExtraServices : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this)
            .RequireAuthorization();

        group
            .MapGet(GetExtraServicesByRenting, "by-renting/{rentingId}", Permissions.ExtraServiceRead)
            .MapPost(CreateExtraService, policy: Permissions.ExtraServiceCreate)
            .MapPut(UpdateExtraService, "{id}", Permissions.ExtraServiceUpdate)
            .MapDelete(DeleteExtraService, "{id}", Permissions.ExtraServiceDelete);
    }

    public async Task<Ok<IList<ExtraServiceDto>>> GetExtraServicesByRenting(ISender sender, int rentingId)
    {
        var result = await sender.Send(new GetExtraServicesByRentingQuery(rentingId));
        return TypedResults.Ok(result);
    }

    public async Task<Created<int>> CreateExtraService(ISender sender, CreateExtraServiceCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/extraservices/{id}", id);
    }

    public async Task<Results<NoContent, BadRequest>> UpdateExtraService(
        ISender sender, int id, UpdateExtraServiceCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);
        return TypedResults.NoContent();
    }

    public async Task<NoContent> DeleteExtraService(ISender sender, int id)
    {
        await sender.Send(new DeleteExtraServiceCommand(id));
        return TypedResults.NoContent();
    }
}
