using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Common.Models;
using RemSolution.Domain.Constants;
using RemSolution.Application.Features.Chat.Commands.MarkChatReadCommand;
using RemSolution.Application.Features.Chat.Commands.SendChatMessageCommand;
using RemSolution.Application.Features.Chat.DTOs;
using RemSolution.Application.Features.Chat.Queries.GetChatMessagesQuery;
using RemSolution.Application.Features.Chat.Queries.GetChatThreadsQuery;

namespace RemSolution.Web.Endpoints;

// The agency side of the renting conversations. The customer side lives on the
// Marketplace group, which authorises by marketplace account rather than by
// agency permission. Delivery is by polling: the SPA re-reads a thread with
// ?afterId= while it is open, so no socket transport is involved.
public class Chat : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this)
            .RequireAuthorization();

        group
            .MapGet(GetThreads, "threads", Permissions.ChatView)
            .MapGet(GetMessages, "threads/{rentingId}", Permissions.ChatView)
            .MapPost(SendMessage, "threads/{rentingId}/messages", Permissions.ChatSend)
            .MapPost(MarkRead, "threads/{rentingId}/read", Permissions.ChatView);
    }

    public async Task<Ok<PaginatedList<ChatThreadDto>>> GetThreads(
        ISender sender, [AsParameters] GetChatThreadsQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Ok<IList<ChatMessageDto>>> GetMessages(
        ISender sender, int rentingId, int? afterId)
    {
        var result = await sender.Send(new GetChatMessagesQuery(rentingId, afterId));
        return TypedResults.Ok(result);
    }

    public async Task<Results<Created<int>, BadRequest>> SendMessage(
        ISender sender, int rentingId, SendChatMessageCommand command)
    {
        if (rentingId != command.RentingId)
            return TypedResults.BadRequest();

        var id = await sender.Send(command);
        return TypedResults.Created($"/chat/threads/{rentingId}", id);
    }

    public async Task<NoContent> MarkRead(ISender sender, int rentingId)
    {
        await sender.Send(new MarkChatReadCommand(rentingId));
        return TypedResults.NoContent();
    }
}
