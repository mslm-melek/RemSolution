using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Features.AgencySubscription.Commands.AssignAgencySubscriptionCommand;
using RemSolution.Application.Features.AgencySubscription.Commands.UpdateAgencySubscriptionCommand;
using RemSolution.Application.Features.AgencySubscription.DTOs;
using RemSolution.Application.Features.AgencySubscription.Queries.GetAgencySubscriptionsQuery;
using RemSolution.Domain.Constants;

namespace RemSolution.Web.Endpoints;

public class AgencySubscriptions : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .RequireAuthorization(Policies.PlatformAdminOnly)
            .MapGet(GetAgencySubscriptions)
            .MapPost(AssignAgencySubscription)
            .MapPut(UpdateAgencySubscription, "{id}");
    }

    public async Task<Ok<IList<AgencySubscriptionDto>>> GetAgencySubscriptions(ISender sender, [AsParameters] GetAgencySubscriptionsQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Created<int>> AssignAgencySubscription(ISender sender, AssignAgencySubscriptionCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/AgencySubscriptions/{id}", id);
    }

    public async Task<Results<NoContent, BadRequest>> UpdateAgencySubscription(ISender sender, int id, UpdateAgencySubscriptionCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }
}
