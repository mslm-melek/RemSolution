using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Domain.Constants;
using RemSolution.Application.Features.Dashboard.DTOs;
using RemSolution.Application.Features.Dashboard.Queries.GetBookingCalendarQuery;
using RemSolution.Application.Features.Dashboard.Queries.GetDashboardQuery;

namespace RemSolution.Web.Endpoints;

public class Dashboard : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this)
            .RequireAuthorization();

        group
            .MapGet(GetDashboard, policy: Permissions.DashboardView)
            .MapGet(GetBookingCalendar, "calendar", Permissions.DashboardView);
    }

    public async Task<Ok<DashboardDto>> GetDashboard(
        ISender sender, [AsParameters] GetDashboardQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    // The pickups, returns and holds falling inside a window — the same overview
    // as the figures above, laid out by day (see the home screen's calendar).
    public async Task<Ok<BookingCalendarDto>> GetBookingCalendar(
        ISender sender, [AsParameters] GetBookingCalendarQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }
}
