using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Domain.Constants;
using RemSolution.Application.Features.ExtraServicesTypeTemplate.Commands.CreateExtraServicesTypeTemplateCommand;
using RemSolution.Application.Features.ExtraServicesTypeTemplate.Commands.DeactivateExtraServicesTypeTemplateCommand;
using RemSolution.Application.Features.ExtraServicesTypeTemplate.Commands.UpdateExtraServicesTypeTemplateCommand;
using RemSolution.Application.Features.ExtraServicesTypeTemplate.DTOs;
using RemSolution.Application.Features.ExtraServicesTypeTemplate.Queries.GetExtraServicesTypeTemplatesQuery;

namespace RemSolution.Web.Endpoints;

// The platform's standard add-on types, copied into every agency.
public class ExtraServiceTypeTemplates : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this)
            .RequireAuthorization(Policies.PlatformAdminOnly);

        group
            .MapGet(GetExtraServiceTypeTemplates)
            .MapPost(CreateExtraServiceTypeTemplate)
            .MapPut(UpdateExtraServiceTypeTemplate, "{id}")
            .MapDelete(DeactivateExtraServiceTypeTemplate, "{id}");
    }

    public async Task<Ok<IList<ExtraServicesTypeTemplateDto>>> GetExtraServiceTypeTemplates(ISender sender)
    {
        var result = await sender.Send(new GetExtraServicesTypeTemplatesQuery());
        return TypedResults.Ok(result);
    }

    public async Task<Created<int>> CreateExtraServiceTypeTemplate(
        ISender sender, CreateExtraServicesTypeTemplateCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/extraservicetypetemplates/{id}", id);
    }

    public async Task<Results<NoContent, BadRequest>> UpdateExtraServiceTypeTemplate(
        ISender sender, int id, UpdateExtraServicesTypeTemplateCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);
        return TypedResults.NoContent();
    }

    public async Task<NoContent> DeactivateExtraServiceTypeTemplate(ISender sender, int id)
    {
        await sender.Send(new DeactivateExtraServicesTypeTemplateCommand(id));
        return TypedResults.NoContent();
    }
}
