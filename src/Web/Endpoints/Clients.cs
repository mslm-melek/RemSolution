using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Features.Client.Commands.CreateClientCommand;
using RemSolution.Application.Features.Client.Commands.DeleteClientCommand;
using RemSolution.Application.Features.Client.Commands.UpdateClientCommand;
using RemSolution.Application.Features.Client.DTOs;
using RemSolution.Application.Features.Client.Commands.UploadClientDocumentCommand;
using RemSolution.Application.Features.Client.Queries.GetClientByIdQuery;
using RemSolution.Application.Features.Client.Queries.GetClientsWithPaginationQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Web.Endpoints;

public class Clients : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this)
            .RequireAuthorization();

        // Each route demands its permission policy (the commands carry the
        // same [Authorize(Policy)] for defence in depth); the agency
        // administrator passes every permission policy by role.
        group
            .MapGet(GetClients, policy: Permissions.ClientRead)
            .MapGet(GetClientById, "{id}", Permissions.ClientRead)
            .MapPost(CreateClient, policy: Permissions.ClientCreate)
            .MapPut(UpdateClient, "{id}", Permissions.ClientUpdate)
            .MapDelete(DeleteClient, "{id}", Permissions.ClientDelete);

        // The only form-binding endpoint in the API; antiforgery middleware is
        // not configured, so form binding must opt out explicitly. The route
        // is cookie-authenticated like every other endpoint in the group.
        // Replacing documents is an edit of the client record: Client.Update.
        group.MapPost("{id}/documents/{documentType}", UploadClientDocument)
            .WithName(nameof(UploadClientDocument))
            .RequireAuthorization(Permissions.ClientUpdate)
            .DisableAntiforgery();
    }

    public async Task<Ok<PaginatedList<ClientDto>>> GetClients(ISender sender, [AsParameters] GetClientsWithPaginationQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Ok<ClientDto>> GetClientById(ISender sender, int id)
    {
        // A missing id surfaces as NotFoundException from the handler; the
        // exception handler turns it into the 404 response.
        var result = await sender.Send(new GetClientByIdQuery(id));

        return TypedResults.Ok(result);
    }

    public async Task<Created<int>> CreateClient(ISender sender, CreateClientCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/clients/{id}", id);
    }

    public async Task<Results<NoContent, BadRequest>> UpdateClient(ISender sender, int id, UpdateClientCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }

    public async Task<NoContent> DeleteClient(ISender sender, int id)
    {
        await sender.Send(new DeleteClientCommand(id));
        return TypedResults.NoContent();
    }

    public async Task<Ok<string>> UploadClientDocument(ISender sender, int id, ClientDocumentType documentType, IFormFile file)
    {
        await using var content = file.OpenReadStream();

        var url = await sender.Send(new UploadClientDocumentCommand
        {
            ClientId = id,
            DocumentType = documentType,
            FileName = file.FileName,
            ContentType = file.ContentType,
            Length = file.Length,
            Content = content
        });

        return TypedResults.Ok(url);
    }
}
