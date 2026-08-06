using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Domain.Constants;
using RemSolution.Application.Features.Dashboard.DTOs;
using RemSolution.Application.Features.Dashboard.Queries.GetBookingCalendarQuery;
using RemSolution.Application.Features.Dashboard.Queries.GetDashboardQuery;
using RemSolution.Application.Features.Dashboard.Queries.GetTodayQuery;

namespace RemSolution.Web.Endpoints;

public class Dashboard : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this)
            .RequireAuthorization();

        group
            .MapGet(GetDashboard, policy: Permissions.DashboardView)
            // The landing screen and its agenda are reachable by every signed-in
            // member of the agency; what they contain is decided section by
            // section inside the queries (see GetTodayQuery).
            .MapGet(GetToday, "today")
            .MapGet(GetBookingCalendar, "calendar");
    }

    public async Task<Ok<DashboardDto>> GetDashboard(
        ISender sender, [AsParameters] GetDashboardQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    // The agency's landing screen in one call: the day's movements, what is waiting
    // on somebody, and what the fleet is doing. Sections the caller may not see are
    // omitted rather than zeroed (see GetTodayQuery).
    public async Task<Ok<TodayDto>> GetToday(
        ISender sender, [AsParameters] GetTodayQuery query)
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
