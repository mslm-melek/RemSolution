using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Features.Client.Commands.CreateClientCommand;
using RemSolution.Application.Features.Client.Commands.DeleteClientCommand;
using RemSolution.Application.Features.Client.Commands.EraseClientPersonalDataCommand;
using RemSolution.Application.Features.Client.Commands.FlagClientCommand;
using RemSolution.Application.Features.Client.Commands.InviteClientCommand;
using RemSolution.Application.Features.Client.Commands.RegenerateClientPortraitCommand;
using RemSolution.Application.Features.Client.Commands.UpdateClientCommand;
using RemSolution.Application.Features.Client.DTOs;
using RemSolution.Application.Features.Client.Commands.UploadClientDocumentCommand;
using RemSolution.Application.Features.Client.Queries.GetClientByIdQuery;
using RemSolution.Application.Features.Client.Queries.GetClientReliabilityQuery;
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
            // Read on Reservation.Read, not Client.Read: it is shown at the
            // moment a hold is answered (see the query).
            .MapGet(GetClientReliability, "{id}/reliability", Permissions.ReservationRead)
            .MapPost(CreateClient, policy: Permissions.ClientCreate)
            .MapPut(UpdateClient, "{id}", Permissions.ClientUpdate)
            // Raising/clearing the bad-client flag is an edit of the client
            // record: Client.Update.
            .MapPut(FlagClient, "{id}/flag", Permissions.ClientUpdate)
            // Sending the customer-portal invitation is likewise an edit of the
            // client record (it writes the account link): Client.Update.
            .MapPost(InviteClient, "{id}/invite", Permissions.ClientUpdate)
            // Re-cutting the portrait out of the CIN already on file replaces a
            // stored file on the client record: Client.Update, like the upload
            // that normally produces it.
            .MapPost(RegenerateClientPortrait, "{id}/portrait", Permissions.ClientUpdate)
            .MapDelete(DeleteClient, "{id}", Permissions.ClientDelete)
            // Its own permission, not Client.Delete: that one archives and can be
            // undone, this one destroys identity documents for good.
            .MapPost(EraseClientPersonalData, "{id}/erase-personal-data", Permissions.ClientErase);

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

    public async Task<Ok<ClientReliabilityDto>> GetClientReliability(ISender sender, int id)
    {
        var result = await sender.Send(new GetClientReliabilityQuery(id));
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

    public async Task<Results<NoContent, BadRequest>> FlagClient(ISender sender, int id, FlagClientCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }

    // Returns what happened rather than a bare 204: "already linked to an
    // account the customer chose their own password for" and "temporary
    // password re-sent" are different answers, and the agency is the one who
    // has to tell the customer which.
    public async Task<Ok<ClientInvitationDto>> InviteClient(ISender sender, int id)
    {
        var result = await sender.Send(new InviteClientCommand(id));

        return TypedResults.Ok(result);
    }

    // Returns the outcome rather than a bare 204: "no face could be found on that
    // image" is a real answer the agency has to be shown, and it is not an error.
    public async Task<Ok<ClientPortraitDto>> RegenerateClientPortrait(ISender sender, int id)
    {
        var result = await sender.Send(new RegenerateClientPortraitCommand(id));

        return TypedResults.Ok(result);
    }

    public async Task<NoContent> DeleteClient(ISender sender, int id)
    {
        await sender.Send(new DeleteClientCommand(id));
        return TypedResults.NoContent();
    }

    // Irreversible. The reason is the operator's own note, kept beside the
    // erasure in the audit trail — see the command.
    public async Task<NoContent> EraseClientPersonalData(
        ISender sender, int id, EraseClientPersonalDataCommand? command)
    {
        await sender.Send(new EraseClientPersonalDataCommand(id, command?.Reason));
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
