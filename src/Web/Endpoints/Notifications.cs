using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Features.Notification.Commands.MarkNotificationsReadCommand;
using RemSolution.Application.Features.Notification.Commands.SendClientLateNoticeCommand;
using RemSolution.Application.Features.Notification.DTOs;
using RemSolution.Application.Features.Notification.Queries.GetMyNotificationsQuery;
using RemSolution.Application.Features.Notification.Queries.GetMyUnreadNotificationCountQuery;
using RemSolution.Domain.Constants;

namespace RemSolution.Web.Endpoints;

// The caller's own inbox, plus the one notification a person sends by hand.
//
// The reads carry no permission policy on purpose: they are scoped to the
// caller's own rows by the handler, and the permission rule was already applied
// when the recipients were chosen (see INotificationService). The feature gate on
// each request is what turns the whole module off for an agency that has not
// subscribed to it.
public class Notifications : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this)
            .RequireAuthorization();

        group
            .MapGet(GetMine)
            .MapGet(GetUnreadCount, "unread-count")
            // Named for the module, not just the action: the generated client is
            // one namespace, and a bare MarkRead already belongs to Chat.
            .MapPost(MarkNotificationsRead, "read")
            // Writing to a customer in the agency's name is a grant of its own.
            .MapPost(SendClientLateNotice, "client-late-notice", Permissions.NotificationSend);
    }

    public async Task<Ok<PaginatedList<NotificationDto>>> GetMine(
        ISender sender, [AsParameters] GetMyNotificationsQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    // Polled by the navigation bell from every screen, so it stays a bare count.
    public async Task<Ok<int>> GetUnreadCount(ISender sender)
    {
        var result = await sender.Send(new GetMyUnreadNotificationCountQuery());
        return TypedResults.Ok(result);
    }

    // Returns how many rows were actually marked, which is what lets the SPA
    // refresh the badge only when something changed.
    public async Task<Ok<int>> MarkNotificationsRead(
        ISender sender, MarkNotificationsReadCommand command)
    {
        var result = await sender.Send(command);
        return TypedResults.Ok(result);
    }

    // 200 with an outcome rather than an error status for a mail that did not go:
    // "this client has no email address" and "already sent today" are answers the
    // screen reports, not failures of the request.
    public async Task<Ok<LateNoticeResult>> SendClientLateNotice(
        ISender sender, SendClientLateNoticeCommand command)
    {
        var result = await sender.Send(command);
        return TypedResults.Ok(result);
    }
}
