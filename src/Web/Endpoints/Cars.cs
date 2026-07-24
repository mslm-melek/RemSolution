using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Common.Models;
using RemSolution.Domain.Constants;
using RemSolution.Application.Features.Car.Commands.CreateCarCommand;
using RemSolution.Application.Features.Car.Commands.DeleteCarCommand;
using RemSolution.Application.Features.Car.Commands.DeleteCarImageCommand;
using RemSolution.Application.Features.Car.Commands.ReorderCarImagesCommand;
using RemSolution.Application.Features.Car.Commands.SetPrimaryCarImageCommand;
using RemSolution.Application.Features.Car.Commands.UpdateCarCommand;
using RemSolution.Application.Features.Car.Commands.UploadCarImageCommand;
using RemSolution.Application.Features.Car.Commands.UploadCarPhotoCommand;
using RemSolution.Application.Features.Car.DTOs;
using RemSolution.Application.Features.Car.Queries.GetCarByIdQuery;
using RemSolution.Application.Features.Car.Queries.GetCarImagesQuery;
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

        // Gallery images (multi-image, with generated thumbnail/medium).
        group.MapGet("{id}/images", GetCarImages)
            .WithName(nameof(GetCarImages))
            .RequireAuthorization(Permissions.CarRead);

        group.MapPost("{id}/images", UploadCarImage)
            .WithName(nameof(UploadCarImage))
            .RequireAuthorization(Permissions.CarUpdate)
            .DisableAntiforgery();

        group.MapPut("{id}/images/{imageId}/primary", SetPrimaryCarImage)
            .WithName(nameof(SetPrimaryCarImage))
            .RequireAuthorization(Permissions.CarUpdate);

        group.MapPut("{id}/images/order", ReorderCarImages)
            .WithName(nameof(ReorderCarImages))
            .RequireAuthorization(Permissions.CarUpdate);

        group.MapDelete("{id}/images/{imageId}", DeleteCarImage)
            .WithName(nameof(DeleteCarImage))
            .RequireAuthorization(Permissions.CarUpdate);
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

    public async Task<Ok<IList<CarImageDto>>> GetCarImages(ISender sender, int id)
    {
        var result = await sender.Send(new GetCarImagesQuery(id));
        return TypedResults.Ok(result);
    }

    public async Task<Ok<CarImageDto>> UploadCarImage(ISender sender, int id, IFormFile file)
    {
        await using var content = file.OpenReadStream();

        var image = await sender.Send(new UploadCarImageCommand
        {
            CarId = id,
            FileName = file.FileName,
            ContentType = file.ContentType,
            Length = file.Length,
            Content = content
        });

        return TypedResults.Ok(image);
    }

    public async Task<NoContent> SetPrimaryCarImage(ISender sender, int id, int imageId)
    {
        await sender.Send(new SetPrimaryCarImageCommand(id, imageId));
        return TypedResults.NoContent();
    }

    public async Task<NoContent> DeleteCarImage(ISender sender, int id, int imageId)
    {
        await sender.Send(new DeleteCarImageCommand(id, imageId));
        return TypedResults.NoContent();
    }

    public async Task<NoContent> ReorderCarImages(ISender sender, int id, List<int> orderedImageIds)
    {
        await sender.Send(new ReorderCarImagesCommand(id, orderedImageIds));
        return TypedResults.NoContent();
    }

}
