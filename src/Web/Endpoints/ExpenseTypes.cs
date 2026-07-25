using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Domain.Constants;
using RemSolution.Application.Features.ExpenseType.Commands.CreateExpenseTypeCommand;
using RemSolution.Application.Features.ExpenseType.Commands.DeactivateExpenseTypeCommand;
using RemSolution.Application.Features.ExpenseType.Commands.UpdateExpenseTypeCommand;
using RemSolution.Application.Features.ExpenseType.DTOs;
using RemSolution.Application.Features.ExpenseType.Queries.GetExpenseTypesQuery;

namespace RemSolution.Web.Endpoints;

public class ExpenseTypes : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this)
            .RequireAuthorization();

        group
            // Read is open to any authenticated user (staff pick a type); managing
            // the catalog is restricted to agency/platform administrators.
            .MapGet(GetExpenseTypes)
            .MapPost(CreateExpenseType, policy: Policies.AgencyOrPlatformAdmin)
            .MapPut(UpdateExpenseType, "{id}", Policies.AgencyOrPlatformAdmin)
            .MapDelete(DeactivateExpenseType, "{id}", Policies.AgencyOrPlatformAdmin);
    }

    public async Task<Ok<IList<ExpenseTypeDto>>> GetExpenseTypes(
        ISender sender, [AsParameters] GetExpenseTypesQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Created<int>> CreateExpenseType(ISender sender, CreateExpenseTypeCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/expensetypes/{id}", id);
    }

    public async Task<Results<NoContent, BadRequest>> UpdateExpenseType(
        ISender sender, int id, UpdateExpenseTypeCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);
        return TypedResults.NoContent();
    }

    public async Task<NoContent> DeactivateExpenseType(ISender sender, int id)
    {
        await sender.Send(new DeactivateExpenseTypeCommand(id));
        return TypedResults.NoContent();
    }
}
