using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Common.Models;
using RemSolution.Domain.Constants;
using RemSolution.Application.Features.Chat.DTOs;
using RemSolution.Application.Features.Marketplace.Commands.CancelMyReservationCommand;
using RemSolution.Application.Features.Marketplace.Commands.CreateCustomerReservationCommand;
using RemSolution.Application.Features.Marketplace.Commands.MarkMyChatReadCommand;
using RemSolution.Application.Features.Marketplace.Commands.SendCustomerChatMessageCommand;
using RemSolution.Application.Features.MarketplaceSearch.DTOs;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMarketplaceCarQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyChatMessagesQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyChatThreadsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyReservationsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.SearchAvailableCarsQuery;

namespace RemSolution.Web.Endpoints;

// The public customer marketplace. Browse is anonymous (no group-level
// RequireAuthorization); the booking / my-reservations actions (added with the
// CustomerOnly policy) require a signed-in Customer.
public class Marketplace : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this);

        group
            .MapGet(SearchCars, "cars")
            .MapGet(GetCar, "cars/{id}")
            // Customer actions require a signed-in Customer.
            .MapPost(BookCar, "reservations", Policies.CustomerOnly)
            .MapGet(GetMyReservations, "my-reservations", Policies.CustomerOnly)
            .MapPost(CancelMyReservation, "reservations/{id}/cancel", Policies.CustomerOnly)
            // The customer half of the renting conversations (the agency half is
            // the Chat group). Same polling contract: re-read with ?afterId=.
            .MapGet(GetMyChatThreads, "my-chats", Policies.CustomerOnly)
            .MapGet(GetMyChatMessages, "my-chats/{rentingId}", Policies.CustomerOnly)
            .MapPost(SendMyChatMessage, "my-chats/{rentingId}/messages", Policies.CustomerOnly)
            .MapPost(MarkMyChatRead, "my-chats/{rentingId}/read", Policies.CustomerOnly);
    }

    public async Task<Ok<PaginatedList<MarketplaceCarDto>>> SearchCars(
        ISender sender, [AsParameters] SearchAvailableCarsQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Results<Ok<MarketplaceCarDto>, NotFound>> GetCar(ISender sender, int id)
    {
        var result = await sender.Send(new GetMarketplaceCarQuery(id));

        if (result is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(result);
    }

    public async Task<Created<int>> BookCar(ISender sender, CreateCustomerReservationCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/marketplace/reservations/{id}", id);
    }

    public async Task<Ok<IList<MyReservationDto>>> GetMyReservations(ISender sender)
    {
        var result = await sender.Send(new GetMyReservationsQuery());
        return TypedResults.Ok(result);
    }

    public async Task<NoContent> CancelMyReservation(ISender sender, int id)
    {
        await sender.Send(new CancelMyReservationCommand(id));
        return TypedResults.NoContent();
    }

    public async Task<Ok<IList<MyChatThreadDto>>> GetMyChatThreads(ISender sender)
    {
        var result = await sender.Send(new GetMyChatThreadsQuery());
        return TypedResults.Ok(result);
    }

    public async Task<Ok<IList<ChatMessageDto>>> GetMyChatMessages(
        ISender sender, int rentingId, int? afterId)
    {
        var result = await sender.Send(new GetMyChatMessagesQuery(rentingId, afterId));
        return TypedResults.Ok(result);
    }

    public async Task<Results<Created<int>, BadRequest>> SendMyChatMessage(
        ISender sender, int rentingId, SendCustomerChatMessageCommand command)
    {
        if (rentingId != command.RentingId)
            return TypedResults.BadRequest();

        var id = await sender.Send(command);
        return TypedResults.Created($"/marketplace/my-chats/{rentingId}", id);
    }

    public async Task<NoContent> MarkMyChatRead(ISender sender, int rentingId)
    {
        await sender.Send(new MarkMyChatReadCommand(rentingId));
        return TypedResults.NoContent();
    }
}
