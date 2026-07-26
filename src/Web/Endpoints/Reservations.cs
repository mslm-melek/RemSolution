using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Common.Models;
using RemSolution.Domain.Constants;
using RemSolution.Application.Features.Reservation.Commands.CancelReservationCommand;
using RemSolution.Application.Features.Reservation.Commands.ConfirmReservationCommand;
using RemSolution.Application.Features.Reservation.Commands.ConvertReservationCommand;
using RemSolution.Application.Features.Reservation.Commands.CreateReservationCommand;
using RemSolution.Application.Features.Reservation.Commands.RejectReservationCommand;
using RemSolution.Application.Features.Reservation.Commands.UpdateReservationCommand;
using RemSolution.Application.Features.Reservation.DTOs;
using RemSolution.Application.Features.Reservation.Queries.GetReservationByIdQuery;
using RemSolution.Application.Features.Reservation.Queries.GetReservationsWithPaginationQuery;

namespace RemSolution.Web.Endpoints;

public class Reservations : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this)
            .RequireAuthorization();

        group
            .MapGet(GetReservations, policy: Permissions.ReservationRead)
            .MapGet(GetReservationById, "{id}", Permissions.ReservationRead)
            .MapPost(CreateReservation, policy: Permissions.ReservationCreate)
            .MapPost(ConfirmReservation, "{id}/confirm", Permissions.ReservationUpdate)
            .MapPost(RejectReservation, "{id}/reject", Permissions.ReservationUpdate)
            .MapPost(ConvertReservation, "{id}/convert", Permissions.ReservationUpdate)
            .MapPut(UpdateReservation, "{id}", Permissions.ReservationUpdate)
            .MapDelete(CancelReservation, "{id}", Permissions.ReservationDelete);
    }

    public async Task<Ok<PaginatedList<ReservationDto>>> GetReservations(
        ISender sender, [AsParameters] GetReservationsWithPaginationQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Results<Ok<ReservationDto>, NotFound>> GetReservationById(ISender sender, int id)
    {
        var result = await sender.Send(new GetReservationByIdQuery(id));

        if (result is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(result);
    }

    public async Task<Created<int>> CreateReservation(ISender sender, CreateReservationCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/reservations/{id}", id);
    }

    // Agency approves the hold (Confirmed). It does NOT create a renting — see
    // ConvertReservation for that.
    public async Task<NoContent> ConfirmReservation(ISender sender, int id)
    {
        await sender.Send(new ConfirmReservationCommand(id));
        return TypedResults.NoContent();
    }

    // Agency declines the hold with a reason shown to the client.
    public async Task<Results<NoContent, BadRequest>> RejectReservation(
        ISender sender, int id, RejectReservationCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);
        return TypedResults.NoContent();
    }

    // Converts a Confirmed/Paid hold into a renting; returns the new renting id.
    public async Task<Results<Ok<int>, BadRequest>> ConvertReservation(
        ISender sender, int id, ConvertReservationCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        var rentingId = await sender.Send(command);
        return TypedResults.Ok(rentingId);
    }

    public async Task<Results<NoContent, BadRequest>> UpdateReservation(
        ISender sender, int id, UpdateReservationCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);
        return TypedResults.NoContent();
    }

    public async Task<NoContent> CancelReservation(ISender sender, int id, string? reason)
    {
        await sender.Send(new CancelReservationCommand(id, reason));
        return TypedResults.NoContent();
    }
}
