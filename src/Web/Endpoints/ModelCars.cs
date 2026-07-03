using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Features.ModelCar.Commands.CreateModelCarCommand;
using RemSolution.Application.Features.ModelCar.Commands.DeleteModelCarCommand;
using RemSolution.Application.Features.ModelCar.Commands.UpdateModelCarCommand;
using RemSolution.Application.Features.ModelCar.DTOs;
using RemSolution.Application.Features.ModelCar.Queries.GetModelCarByIdQuery;
using RemSolution.Application.Features.ModelCar.Queries.GetModelCarsWithPaginationQuery;

namespace RemSolution.Web.Endpoints;

public class ModelCars : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .RequireAuthorization()
            .MapGet(GetModelCars)
            .MapGet(GetModelCarById, "{id}")
            .MapPost(CreateModelCar)
            .MapPut(UpdateModelCar, "{id}")
            .MapDelete(DeleteModelCar, "{id}");
    }

    public async Task<Ok<PaginatedList<ModelCarDto>>> GetModelCars(ISender sender, [AsParameters] GetModelCarsWithPaginationQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Results<Ok<ModelCarDto>, NotFound>> GetModelCarById(ISender sender, int id)
    {
        var result = await sender.Send(new GetModelCarByIdQuery(id));

        if (result is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(result);
    }

    public async Task<Created<int>> CreateModelCar(ISender sender, CreateModelCarCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/ModelCars/{id}", id);
    }

    public async Task<Results<NoContent, BadRequest>> UpdateModelCar(ISender sender, int id, UpdateModelCarCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }

    public async Task<NoContent> DeleteModelCar(ISender sender, int id)
    {
        await sender.Send(new DeleteModelCarCommand(id));
        return TypedResults.NoContent();
    }

}
