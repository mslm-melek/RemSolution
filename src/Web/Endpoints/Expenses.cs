using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Common.Models;
using RemSolution.Domain.Constants;
using RemSolution.Application.Features.Expense.Commands.CreateExpenseCommand;
using RemSolution.Application.Features.Expense.Commands.DeleteExpenseCommand;
using RemSolution.Application.Features.Expense.Commands.RecordExpensePaymentCommand;
using RemSolution.Application.Features.Expense.Commands.UpdateExpenseCommand;
using RemSolution.Application.Features.Expense.DTOs;
using RemSolution.Application.Features.Expense.Queries.GetExpenseByIdQuery;
using RemSolution.Application.Features.Expense.Queries.GetExpensesWithPaginationQuery;

namespace RemSolution.Web.Endpoints;

public class Expenses : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this)
            .RequireAuthorization();

        group
            .MapGet(GetExpenses, policy: Permissions.ExpenseRead)
            .MapGet(GetExpenseById, "{id}", Permissions.ExpenseRead)
            .MapPost(CreateExpense, policy: Permissions.ExpenseCreate)
            // Settling an expense is an update of its running paid total, so it
            // rides on ExpenseUpdate rather than a permission of its own.
            .MapPost(RecordExpensePayment, "{id}/payments", Permissions.ExpenseUpdate)
            .MapPut(UpdateExpense, "{id}", Permissions.ExpenseUpdate)
            .MapDelete(DeleteExpense, "{id}", Permissions.ExpenseDelete);
    }

    public async Task<Ok<PaginatedList<ExpenseDto>>> GetExpenses(
        ISender sender, [AsParameters] GetExpensesWithPaginationQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Results<Ok<ExpenseDto>, NotFound>> GetExpenseById(ISender sender, int id)
    {
        var result = await sender.Send(new GetExpenseByIdQuery(id));

        if (result is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(result);
    }

    public async Task<Created<int>> CreateExpense(ISender sender, CreateExpenseCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/expenses/{id}", id);
    }

    public async Task<Results<NoContent, BadRequest>> UpdateExpense(
        ISender sender, int id, UpdateExpenseCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);
        return TypedResults.NoContent();
    }

    public async Task<Results<NoContent, BadRequest>> RecordExpensePayment(
        ISender sender, int id, RecordExpensePaymentCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);
        return TypedResults.NoContent();
    }

    public async Task<NoContent> DeleteExpense(ISender sender, int id)
    {
        await sender.Send(new DeleteExpenseCommand(id));
        return TypedResults.NoContent();
    }
}
