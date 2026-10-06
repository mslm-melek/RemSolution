using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Domain.Constants;
using RemSolution.Application.Features.ExpenseTypeTemplate.Commands.CreateExpenseTypeTemplateCommand;
using RemSolution.Application.Features.ExpenseTypeTemplate.Commands.DeactivateExpenseTypeTemplateCommand;
using RemSolution.Application.Features.ExpenseTypeTemplate.Commands.UpdateExpenseTypeTemplateCommand;
using RemSolution.Application.Features.ExpenseTypeTemplate.DTOs;
using RemSolution.Application.Features.ExpenseTypeTemplate.Queries.GetExpenseTypeTemplatesQuery;

namespace RemSolution.Web.Endpoints;

// The platform's standard expense types, copied into every agency.
public class ExpenseTypeTemplates : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this)
            .RequireAuthorization(Policies.PlatformAdminOnly);

        group
            .MapGet(GetExpenseTypeTemplates)
            .MapPost(CreateExpenseTypeTemplate)
            .MapPut(UpdateExpenseTypeTemplate, "{id}")
            .MapDelete(DeactivateExpenseTypeTemplate, "{id}");
    }

    public async Task<Ok<IList<ExpenseTypeTemplateDto>>> GetExpenseTypeTemplates(ISender sender)
    {
        var result = await sender.Send(new GetExpenseTypeTemplatesQuery());
        return TypedResults.Ok(result);
    }

    public async Task<Created<int>> CreateExpenseTypeTemplate(
        ISender sender, CreateExpenseTypeTemplateCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/expensetypetemplates/{id}", id);
    }

    public async Task<Results<NoContent, BadRequest>> UpdateExpenseTypeTemplate(
        ISender sender, int id, UpdateExpenseTypeTemplateCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);
        return TypedResults.NoContent();
    }

    public async Task<NoContent> DeactivateExpenseTypeTemplate(ISender sender, int id)
    {
        await sender.Send(new DeactivateExpenseTypeTemplateCommand(id));
        return TypedResults.NoContent();
    }
}
