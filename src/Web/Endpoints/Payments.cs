using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Common.Models;
using RemSolution.Domain.Constants;
using RemSolution.Application.Features.Payment.Commands.CreatePaymentCommand;
using RemSolution.Application.Features.Payment.Commands.ReversePaymentCommand;
using RemSolution.Application.Features.Payment.Commands.UpdatePaymentCommand;
using RemSolution.Application.Features.Payment.Commands.UploadPaymentProofCommand;
using RemSolution.Application.Features.Payment.DTOs;
using RemSolution.Application.Features.Payment.Queries.GetClientBalanceQuery;
using RemSolution.Application.Features.Payment.Queries.GetPaymentByIdQuery;
using RemSolution.Application.Features.Payment.Queries.GetPaymentsWithPaginationQuery;

namespace RemSolution.Web.Endpoints;

public class Payments : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this)
            .RequireAuthorization();

        group
            .MapGet(GetPayments, policy: Permissions.PaymentRead)
            .MapGet(GetClientBalance, "balance/{clientId}", Permissions.PaymentRead)
            .MapGet(GetPaymentById, "{id}", Permissions.PaymentRead)
            .MapPost(CreatePayment, policy: Permissions.PaymentCreate)
            .MapPost(ReversePayment, "{id}/reverse", Permissions.PaymentDelete)
            .MapPut(UpdatePayment, "{id}", Permissions.PaymentUpdate);

        // Form-binding route (like the client-document upload): antiforgery
        // middleware is not configured, so form binding must opt out explicitly.
        // Attaching the proof is an edit of the entry: Payment.Update.
        group.MapPost("{id}/proof", UploadPaymentProof)
            .WithName(nameof(UploadPaymentProof))
            .RequireAuthorization(Permissions.PaymentUpdate)
            .DisableAntiforgery();
    }

    public async Task<Results<Ok<ClientBalanceDto>, NotFound>> GetClientBalance(ISender sender, int clientId)
    {
        var result = await sender.Send(new GetClientBalanceQuery(clientId));

        if (result is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(result);
    }

    public async Task<Ok<PaginatedList<PaymentDto>>> GetPayments(
        ISender sender, [AsParameters] GetPaymentsWithPaginationQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Results<Ok<PaymentDto>, NotFound>> GetPaymentById(ISender sender, int id)
    {
        var result = await sender.Send(new GetPaymentByIdQuery(id));

        if (result is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(result);
    }

    public async Task<Created<int>> CreatePayment(ISender sender, CreatePaymentCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/payments/{id}", id);
    }

    // "Delete" posts an offsetting reversal entry; returns the reversal id.
    public async Task<Ok<int>> ReversePayment(ISender sender, int id)
    {
        var reversalId = await sender.Send(new ReversePaymentCommand(id));
        return TypedResults.Ok(reversalId);
    }

    public async Task<Results<NoContent, BadRequest>> UpdatePayment(
        ISender sender, int id, UpdatePaymentCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);
        return TypedResults.NoContent();
    }

    // Attaches (or replaces) the receipt / transfer slip / invoice kept as proof
    // of this entry; returns the stored file's public URL.
    public async Task<Ok<string>> UploadPaymentProof(ISender sender, int id, IFormFile file)
    {
        await using var content = file.OpenReadStream();

        var url = await sender.Send(new UploadPaymentProofCommand
        {
            PaymentId = id,
            FileName = file.FileName,
            ContentType = file.ContentType,
            Length = file.Length,
            Content = content
        });

        return TypedResults.Ok(url);
    }
}
