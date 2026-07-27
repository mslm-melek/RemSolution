using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using RemSolution.Application.Features.DocumentTemplate.Commands.CreateDocumentTemplateCommand;
using RemSolution.Application.Features.DocumentTemplate.Commands.ImportDocumentTemplateCommand;
using RemSolution.Application.Features.DocumentTemplate.Commands.SetDefaultDocumentTemplateCommand;
using RemSolution.Application.Features.DocumentTemplate.Commands.SetDocumentTemplateActiveCommand;
using RemSolution.Application.Features.DocumentTemplate.Commands.UpdateDocumentTemplateCommand;
using RemSolution.Application.Features.DocumentTemplate.DTOs;
using RemSolution.Application.Features.DocumentTemplate.Queries.GetDocumentPlaceholdersQuery;
using RemSolution.Application.Features.DocumentTemplate.Queries.GetDocumentPromptQuery;
using RemSolution.Application.Features.DocumentTemplate.Queries.GetDocumentTemplateByIdQuery;
using RemSolution.Application.Features.DocumentTemplate.Queries.GetDocumentTemplateExamplesQuery;
using RemSolution.Application.Features.DocumentTemplate.Queries.GetDocumentTemplatesQuery;

namespace RemSolution.Web.Endpoints;

public class DocumentTemplates : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        // Writes are administrator-only (the requests carry the policy too, for
        // defence in depth); the list and the prompt are readable by any agent who
        // may read rentings, because they drive the generate flow on the renting
        // form. The per-kind FEATURE check happens inside the handlers, where the
        // template's kind is known.
        var group = app.MapGroup(this)
            .RequireAuthorization();

        group
            .MapGet(GetDocumentTemplates, policy: Permissions.RentingRead)
            .MapGet(GetDocumentTemplateExamples, "examples", Policies.AgencyOrPlatformAdmin)
            .MapGet(GetDocumentPlaceholders, "placeholders", Policies.AgencyOrPlatformAdmin)
            .MapGet(GetDocumentPrompt, "prompt", Permissions.RentingRead)
            .MapGet(GetDocumentTemplateById, "{id}", Policies.AgencyOrPlatformAdmin)
            .MapPost(CreateDocumentTemplate, policy: Policies.AgencyOrPlatformAdmin)
            .MapPut(UpdateDocumentTemplate, "{id}", Policies.AgencyOrPlatformAdmin)
            .MapPut(SetDefaultDocumentTemplate, "{id}/default", Policies.AgencyOrPlatformAdmin)
            .MapPut(SetDocumentTemplateActive, "{id}/active", Policies.AgencyOrPlatformAdmin);

        // Form-binding upload (mirrors the client-document and car-image uploads):
        // antiforgery middleware is not configured, so form binding must opt out.
        group.MapPost("import", ImportDocumentTemplate)
            .WithName(nameof(ImportDocumentTemplate))
            .RequireAuthorization(Policies.AgencyOrPlatformAdmin)
            .DisableAntiforgery();
    }

    public async Task<Ok<IList<DocumentTemplateDto>>> GetDocumentTemplates(
        ISender sender, [AsParameters] GetDocumentTemplatesQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    // No language argument: examples come back in the caller's own language (see
    // GetDocumentTemplateExamplesQuery).
    public async Task<Ok<IList<DocumentTemplateExampleDto>>> GetDocumentTemplateExamples(ISender sender)
    {
        var result = await sender.Send(new GetDocumentTemplateExamplesQuery());
        return TypedResults.Ok(result);
    }

    public async Task<Ok<IList<DocumentPlaceholderDto>>> GetDocumentPlaceholders(
        ISender sender, DocumentTemplateKind kind)
    {
        var result = await sender.Send(new GetDocumentPlaceholdersQuery(kind));
        return TypedResults.Ok(result);
    }

    public async Task<Ok<IList<DocumentTemplateFieldDto>>> GetDocumentPrompt(
        ISender sender, DocumentTemplateKind kind, int? templateId)
    {
        var result = await sender.Send(new GetDocumentPromptQuery(kind, templateId));
        return TypedResults.Ok(result);
    }

    public async Task<Results<Ok<DocumentTemplateDto>, NotFound>> GetDocumentTemplateById(ISender sender, int id)
    {
        var result = await sender.Send(new GetDocumentTemplateByIdQuery(id));

        if (result is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(result);
    }

    public async Task<Created<int>> CreateDocumentTemplate(ISender sender, CreateDocumentTemplateCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/documenttemplates/{id}", id);
    }

    public async Task<Results<NoContent, BadRequest>> UpdateDocumentTemplate(
        ISender sender, int id, UpdateDocumentTemplateCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);
        return TypedResults.NoContent();
    }

    public async Task<NoContent> SetDefaultDocumentTemplate(ISender sender, int id)
    {
        await sender.Send(new SetDefaultDocumentTemplateCommand(id));
        return TypedResults.NoContent();
    }

    public async Task<NoContent> SetDocumentTemplateActive(ISender sender, int id, bool isActive)
    {
        await sender.Send(new SetDocumentTemplateActiveCommand(id, isActive));
        return TypedResults.NoContent();
    }

    public async Task<Ok<DocumentTemplateDraftDto>> ImportDocumentTemplate(
        ISender sender, DocumentTemplateKind kind, string language, IFormFile file)
    {
        await using var content = file.OpenReadStream();

        var draft = await sender.Send(new ImportDocumentTemplateCommand
        {
            Kind = kind,
            Language = language,
            FileName = file.FileName,
            ContentType = file.ContentType,
            Length = file.Length,
            Content = content
        });

        return TypedResults.Ok(draft);
    }
}
