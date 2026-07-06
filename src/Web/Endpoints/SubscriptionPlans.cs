using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Features.SubscriptionPlan.Commands.CreateSubscriptionPlanCommand;
using RemSolution.Application.Features.SubscriptionPlan.Commands.DeleteSubscriptionPlanCommand;
using RemSolution.Application.Features.SubscriptionPlan.Commands.UpdateSubscriptionPlanCommand;
using RemSolution.Application.Features.SubscriptionPlan.DTOs;
using RemSolution.Application.Features.SubscriptionPlan.Queries.GetSubscriptionPlansQuery;
using RemSolution.Domain.Constants;

namespace RemSolution.Web.Endpoints;

public class SubscriptionPlans : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .RequireAuthorization(Policies.PlatformAdminOnly)
            .MapGet(GetSubscriptionPlans)
            .MapPost(CreateSubscriptionPlan)
            .MapPut(UpdateSubscriptionPlan, "{id}")
            .MapDelete(DeleteSubscriptionPlan, "{id}");
    }

    public async Task<Ok<IList<SubscriptionPlanDto>>> GetSubscriptionPlans(ISender sender)
    {
        var result = await sender.Send(new GetSubscriptionPlansQuery());
        return TypedResults.Ok(result);
    }

    public async Task<Created<int>> CreateSubscriptionPlan(ISender sender, CreateSubscriptionPlanCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/SubscriptionPlans/{id}", id);
    }

    public async Task<Results<NoContent, BadRequest>> UpdateSubscriptionPlan(ISender sender, int id, UpdateSubscriptionPlanCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }

    public async Task<NoContent> DeleteSubscriptionPlan(ISender sender, int id)
    {
        await sender.Send(new DeleteSubscriptionPlanCommand(id));
        return TypedResults.NoContent();
    }
}
