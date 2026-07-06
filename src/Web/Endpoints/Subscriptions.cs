using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Features.AgencySubscription.Queries.GetMySubscriptionQuery;

namespace RemSolution.Web.Endpoints;

/// <summary>
/// Agency-facing (any authenticated user): the caller's own subscription and
/// quota usage. Platform-admin management lives in AgencySubscriptions.
/// </summary>
public class Subscriptions : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .RequireAuthorization()
            .MapGet(GetMySubscription, "my");
    }

    public async Task<Results<Ok<MySubscriptionDto>, NotFound>> GetMySubscription(ISender sender)
    {
        var result = await sender.Send(new GetMySubscriptionQuery());

        if (result is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(result);
    }
}
