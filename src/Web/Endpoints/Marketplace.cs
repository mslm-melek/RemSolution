using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using RemSolution.Application.Common.Models;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using RemSolution.Application.Features.Chat.DTOs;
using RemSolution.Application.Features.Marketplace.Commands.CancelMyReservationCommand;
using RemSolution.Application.Features.Marketplace.Commands.CreateCustomerReservationCommand;
using RemSolution.Application.Features.Marketplace.Commands.CreateMyReviewCommand;
using RemSolution.Application.Features.Marketplace.Commands.MarkMyChatReadCommand;
using RemSolution.Application.Features.Marketplace.Commands.SendCustomerChatMessageCommand;
using RemSolution.Application.Features.Marketplace.Commands.SubmitMyReservationRequirementCommand;
using RemSolution.Application.Features.MarketplaceSearch.DTOs;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyReservationRequirementsQuery;
using RemSolution.Application.Features.Reservation.DTOs;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetAgencyReviewsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMarketplaceAgencyQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMarketplaceCarQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMarketplaceDestinationsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyChatMessagesQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyChatThreadsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyRentingsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyReservationsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetShowcaseCarsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.SearchAvailableCarsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.SearchCarsMapQuery;

namespace RemSolution.Web.Endpoints;

// The public customer marketplace. Browse is anonymous (no group-level
// RequireAuthorization); the booking / my-reservations actions (added with the
// CustomerOnly policy) require a signed-in Customer.
public class Marketplace : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        // The whole group, not just the anonymous half: an authenticated
        // customer hits the same expensive search.
        var group = app.MapGroup(this)
            .RequireRateLimiting(RateLimitPolicies.PublicBrowse);

        group
            .MapGet(SearchCars, "cars")
            .MapGet(GetCar, "cars/{id}")
            // Sibling of "cars", not "cars/map": a literal would sit under the
            // {id} route and read as a car called "map".
            .MapGet(SearchCarsOnMap, "map")
            .MapGet(GetShowcaseCars, "showcase")
            .MapGet(GetDestinations, "destinations")
            .MapGet(GetAgency, "agencies/{id}")
            .MapGet(GetAgencyReviews, "agencies/{id}/reviews")
            // Customer actions require a signed-in Customer.
            .MapPost(BookCar, "reservations", Policies.CustomerOnly)
            .MapGet(GetMyReservations, "my-reservations", Policies.CustomerOnly)
            .MapPost(CancelMyReservation, "reservations/{id}/cancel", Policies.CustomerOnly)
            // "My trips" and the rating a finished trip can carry.
            .MapGet(GetMyRentings, "my-rentings", Policies.CustomerOnly)
            .MapPost(ReviewMyRenting, "my-rentings/{rentingId}/review", Policies.CustomerOnly)
            // The customer half of the renting conversations (the agency half is
            // the Chat group). Same polling contract: re-read with ?afterId=.
            .MapGet(GetMyChatThreads, "my-chats", Policies.CustomerOnly)
            .MapGet(GetMyChatMessages, "my-chats/{subject}/{id}", Policies.CustomerOnly)
            .MapPost(SendMyChatMessage, "my-chats/{subject}/{id}/messages", Policies.CustomerOnly)
            .MapPost(MarkMyChatRead, "my-chats/{subject}/{id}/read", Policies.CustomerOnly)
            // What the agency asked for before pickup, and the customer's answers.
            .MapGet(GetMyReservationRequirements,
                "my-reservations/{id}/requirements", Policies.CustomerOnly);

        // Answering an ask carries a file (a transfer slip, a scan), so it binds
        // a form — and antiforgery middleware is not configured, so form binding
        // has to opt out explicitly, exactly as the other upload endpoints do.
        group.MapPost("my-reservations/requirements/{id}/submit", SubmitMyReservationRequirement)
            .WithName(nameof(SubmitMyReservationRequirement))
            .RequireAuthorization(Policies.CustomerOnly)
            .DisableAntiforgery();
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

    // The same search, reduced to one pin per pick-up place. Separate from
    // SearchCars because the two are read differently: the list is paged, the map
    // is not (see SearchCarsMapQuery).
    public async Task<Ok<IList<MarketplaceMapPointDto>>> SearchCarsOnMap(
        ISender sender, [AsParameters] SearchCarsMapQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    // The home-page slideshow: a handful of cars on offer, no dates involved.
    public async Task<Ok<IList<MarketplaceCarDto>>> GetShowcaseCars(
        ISender sender, [AsParameters] GetShowcaseCarsQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    // Countries and pick-up places that have cars on offer — the "where" half of
    // the search bar.
    public async Task<Ok<IList<MarketplaceDestinationDto>>> GetDestinations(ISender sender)
    {
        var result = await sender.Send(new GetMarketplaceDestinationsQuery());
        return TypedResults.Ok(result);
    }

    public async Task<Results<Ok<MarketplaceAgencyDto>, NotFound>> GetAgency(ISender sender, int id)
    {
        var result = await sender.Send(new GetMarketplaceAgencyQuery(id));

        if (result is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(result);
    }

    // Public: the ratings behind the agency's average. Paged — a well-reviewed
    // agency accumulates hundreds and the shopfront shows the first page.
    public async Task<Ok<PaginatedList<AgencyReviewDto>>> GetAgencyReviews(
        ISender sender, int id, int? pageNumber, int? pageSize)
    {
        var result = await sender.Send(
            new GetAgencyReviewsQuery(id, pageNumber ?? 1, pageSize ?? 10));

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

    // The reason is optional but asked for: a customer who says why is telling
    // the agency something it can act on, and it is shown on the hold.
    public async Task<NoContent> CancelMyReservation(ISender sender, int id, string? reason)
    {
        await sender.Send(new CancelMyReservationCommand(id, reason));
        return TypedResults.NoContent();
    }

    public async Task<Ok<IList<ReservationRequirementDto>>> GetMyReservationRequirements(
        ISender sender, int id)
    {
        var result = await sender.Send(new GetMyReservationRequirementsQuery(id));
        return TypedResults.Ok(result);
    }

    // The file is optional: accepting terms is an answer with nothing attached.
    public async Task<NoContent> SubmitMyReservationRequirement(
        ISender sender, int id, IFormFile? file, [FromForm] string? note)
    {
        await using var content = file?.OpenReadStream();

        await sender.Send(new SubmitMyReservationRequirementCommand
        {
            Id = id,
            Note = note,
            FileName = file?.FileName,
            ContentType = file?.ContentType,
            Content = content
        });

        return TypedResults.NoContent();
    }

    public async Task<Ok<IList<MyRentingDto>>> GetMyRentings(ISender sender)
    {
        var result = await sender.Send(new GetMyRentingsQuery());
        return TypedResults.Ok(result);
    }

    public async Task<Results<Created<int>, BadRequest>> ReviewMyRenting(
        ISender sender, int rentingId, CreateMyReviewCommand command)
    {
        if (rentingId != command.RentingId)
            return TypedResults.BadRequest();

        var id = await sender.Send(command);
        return TypedResults.Created($"/marketplace/agencies/reviews/{id}", id);
    }

    public async Task<Ok<IList<MyChatThreadDto>>> GetMyChatThreads(ISender sender)
    {
        var result = await sender.Send(new GetMyChatThreadsQuery());
        return TypedResults.Ok(result);
    }

    public async Task<Ok<IList<ChatMessageDto>>> GetMyChatMessages(
        ISender sender, ChatSubjectKind subject, int id, int? afterId)
    {
        var result = await sender.Send(new GetMyChatMessagesQuery(subject, id, afterId));
        return TypedResults.Ok(result);
    }

    public async Task<Results<Created<int>, BadRequest>> SendMyChatMessage(
        ISender sender, ChatSubjectKind subject, int id, SendCustomerChatMessageCommand command)
    {
        if (id != command.Id || subject != command.Subject)
            return TypedResults.BadRequest();

        var messageId = await sender.Send(command);
        return TypedResults.Created($"/marketplace/my-chats/{subject}/{id}", messageId);
    }

    public async Task<NoContent> MarkMyChatRead(ISender sender, ChatSubjectKind subject, int id)
    {
        await sender.Send(new MarkMyChatReadCommand(subject, id));
        return TypedResults.NoContent();
    }
}
