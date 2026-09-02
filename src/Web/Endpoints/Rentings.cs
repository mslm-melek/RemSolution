using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Common.Models;
using RemSolution.Domain.Constants;
using RemSolution.Application.Features.Renting.Commands.AddRentingFeeCommand;
using RemSolution.Application.Features.Renting.Commands.CancelRentingCommand;
using RemSolution.Application.Features.Renting.Commands.ChangeRentingEndDateCommand;
using RemSolution.Application.Features.Renting.Commands.ChangeRentingStateCommand;
using RemSolution.Application.Features.Renting.Commands.CreateRentingCommand;
using RemSolution.Application.Features.Renting.Commands.DeleteRentingFeeCommand;
using RemSolution.Application.Features.Renting.Commands.SettleRentingDepositCommand;
using RemSolution.Application.Features.Renting.Commands.UpdateRentingCommand;
using RemSolution.Application.Features.Renting.DTOs;
using RemSolution.Application.Features.Renting.Queries.GetRentingByIdQuery;
using RemSolution.Application.Features.Renting.Queries.GetRentingFeesQuery;
using RemSolution.Application.Features.Renting.Queries.GetRentingHistoryQuery;
using RemSolution.Application.Features.Renting.Queries.GetRentingQuoteQuery;
using RemSolution.Application.Features.Renting.Queries.GetRentingsWithPaginationQuery;

namespace RemSolution.Web.Endpoints;

public class Rentings : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this)
            .RequireAuthorization();

        group
            .MapGet(GetRentings, policy: Permissions.RentingRead)
            // Literal segment, so it is matched ahead of "{id}" whatever the order.
            .MapGet(GetRentingQuote, "quote", Permissions.RentingRead)
            .MapGet(GetRentingById, "{id}", Permissions.RentingRead)
            .MapGet(GetRentingHistory, "{id}/history", Permissions.RentingRead)
            .MapGet(GetRentingFees, "{id}/fees", Permissions.RentingRead)
            .MapPost(CreateRenting, policy: Permissions.RentingCreate)
            // Correcting the charges of a hire already returned. The usual case —
            // charges found AS the car comes back — rides on the state change
            // below instead, so the return is one write.
            .MapPost(AddRentingFee, "{id}/fees", Permissions.RentingUpdate)
            .MapDelete(DeleteRentingFee, "{id}/fees/{feeId}", Permissions.RentingUpdate)
            .MapPut(UpdateRenting, "{id}", Permissions.RentingUpdate)
            .MapPut(ChangeRentingState, "{id}/state", Permissions.RentingUpdate)
            .MapPut(ChangeRentingEndDate, "{id}/end-date", Permissions.RentingUpdate)
            // Cancelling is a transition with decisions attached (the fee kept,
            // whether the excess is refunded), so it takes a body like the two
            // above rather than being a bare DELETE. Still Renting.Delete: for a
            // financial record, cancelling IS deleting (P.11).
            .MapPut(CancelRenting, "{id}/cancel", Permissions.RentingDelete)
            // Saying what became of the deposit when the return did not. Payment
            // rather than Renting permission: it hands money back and writes the
            // refund to the ledger (see SettleRentingDepositCommand).
            .MapPut(SettleRentingDeposit, "{id}/deposit", Permissions.PaymentCreate);
    }

    public async Task<Ok<PaginatedList<RentingDto>>> GetRentings(
        ISender sender, [AsParameters] GetRentingsWithPaginationQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    // What the booking about to be made would cost, and whether the car is free
    // for it. Read-only: the create/update handlers price again for themselves.
    public async Task<Ok<RentingQuoteDto>> GetRentingQuote(
        ISender sender, [AsParameters] GetRentingQuoteQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Results<Ok<RentingDto>, NotFound>> GetRentingById(ISender sender, int id)
    {
        var result = await sender.Send(new GetRentingByIdQuery(id));

        if (result is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(result);
    }

    public async Task<Ok<IList<RentingHistoryDto>>> GetRentingHistory(ISender sender, int id)
    {
        var result = await sender.Send(new GetRentingHistoryQuery(id));
        return TypedResults.Ok(result);
    }

    public async Task<Ok<IList<RentingFeeDto>>> GetRentingFees(ISender sender, int id)
    {
        var result = await sender.Send(new GetRentingFeesQuery(id));
        return TypedResults.Ok(result);
    }

    public async Task<Results<Created<int>, BadRequest>> AddRentingFee(
        ISender sender, int id, AddRentingFeeCommand command)
    {
        if (id != command.RentingId)
            return TypedResults.BadRequest();

        var feeId = await sender.Send(command);
        return TypedResults.Created($"/rentings/{id}/fees/{feeId}", feeId);
    }

    public async Task<NoContent> DeleteRentingFee(ISender sender, int id, int feeId)
    {
        await sender.Send(new DeleteRentingFeeCommand(id, feeId));
        return TypedResults.NoContent();
    }

    public async Task<Created<int>> CreateRenting(ISender sender, CreateRentingCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/rentings/{id}", id);
    }

    public async Task<Results<NoContent, BadRequest>> UpdateRenting(
        ISender sender, int id, UpdateRentingCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);
        return TypedResults.NoContent();
    }

    public async Task<Results<NoContent, BadRequest>> ChangeRentingState(
        ISender sender, int id, ChangeRentingStateCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);
        return TypedResults.NoContent();
    }

    // Extending or shortening a live renting: re-prices the change and,
    // optionally, issues a new contract covering the new period.
    public async Task<Results<NoContent, BadRequest>> ChangeRentingEndDate(
        ISender sender, int id, ChangeRentingEndDateCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);
        return TypedResults.NoContent();
    }

    public async Task<Results<NoContent, BadRequest>> CancelRenting(
        ISender sender, int id, CancelRentingCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);
        return TypedResults.NoContent();
    }

    public async Task<Results<NoContent, BadRequest>> SettleRentingDeposit(
        ISender sender, int id, SettleRentingDepositCommand command)
    {
        if (id != command.RentingId)
            return TypedResults.BadRequest();

        await sender.Send(command);
        return TypedResults.NoContent();
    }
}
