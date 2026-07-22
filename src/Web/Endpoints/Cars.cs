using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Common.Models;
using RemSolution.Domain.Constants;
using RemSolution.Application.Features.Car.Commands.CreateCarCommand;
using RemSolution.Application.Features.Car.Commands.DeleteCarCommand;
using RemSolution.Application.Features.Car.Commands.UpdateCarCommand;
using RemSolution.Application.Features.Car.Commands.UploadCarPhotoCommand;
using RemSolution.Application.Features.Car.DTOs;
using RemSolution.Application.Features.Car.Queries.GetCarByIdQuery;
using RemSolution.Application.Features.Car.Queries.GetCarsWithPaginationQuery;

namespace RemSolution.Web.Endpoints;

public class Cars : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        // Each route demands its permission policy (the commands carry the
        // same [Authorize(Policy)] for defence in depth); the agency
        // administrator passes every permission policy by role.
        var group = app.MapGroup(this)
            .RequireAuthorization();

        group
            .MapGet(GetCars, policy: Permissions.CarRead)
            .MapGet(GetCarById, "{id}", Permissions.CarRead)
            .MapPost(CreateCar, policy: Permissions.CarCreate)
            .MapPut(UpdateCar, "{id}", Permissions.CarUpdate)
            .MapDelete(DeleteCar, "{id}", Permissions.CarDelete);

        // Form-binding upload endpoint (mirrors the client-document upload):
        // antiforgery middleware is not configured, so form binding must opt out
        // explicitly. Setting a car's photo is an edit: Car.Update.
        group.MapPost("{id}/photo", UploadCarPhoto)
            .WithName(nameof(UploadCarPhoto))
            .RequireAuthorization(Permissions.CarUpdate)
            .DisableAntiforgery();
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

    public async Task<Ok<string>> UploadCarPhoto(ISender sender, int id, IFormFile file)
    {
        await using var content = file.OpenReadStream();

        var url = await sender.Send(new UploadCarPhotoCommand
        {
            CarId = id,
            FileName = file.FileName,
            ContentType = file.ContentType,
            Length = file.Length,
            Content = content
        });

        return TypedResults.Ok(url);
    }

}
