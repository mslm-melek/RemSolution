using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Features.Car.Commands.CreateCarCommand;
using RemSolution.Application.Features.Car.Commands.DeleteCarCommand;
using RemSolution.Application.Features.Car.Commands.UpdateCarCommand;
using RemSolution.Application.Features.Car.DTOs;
using RemSolution.Application.Features.Car.Queries.GetCarByIdQuery;
using RemSolution.Application.Features.Car.Queries.GetCarsWithPaginationQuery;

namespace RemSolution.Web.Endpoints;

public class Cars : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .RequireAuthorization()
            .MapGet(GetCars)        
            .MapGet(GetCarById, "{id}")
            .MapPost(CreateCar)
            .MapPut(UpdateCar, "{id}")
            .MapDelete(DeleteCar, "{id}");
    }

    public async Task<Ok<PaginatedList<CarDto>>> GetCars(ISender sender, [AsParameters] GetCarsWithPaginationQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Results<Ok<CarDto>, NotFound>> GetCarById(ISender sender, int id)
    {
        var result = await sender.Send(new GetCarByIdQuery(id));

        if (result is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(result);
    }

    public async Task<Created<int>> CreateCar(ISender sender, CreateCarCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/cars/{id}", id);
    }

    public async Task<Results<NoContent, BadRequest>> UpdateCar(ISender sender, int id, UpdateCarCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }

    public async Task<NoContent> DeleteCar(ISender sender, int id)
    {
        await sender.Send(new DeleteCarCommand(id));
        return TypedResults.NoContent();
    }

}
