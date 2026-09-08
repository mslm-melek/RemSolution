using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Features.AgencyReport.DTOs;
using RemSolution.Application.Features.AgencyReport.Queries.GetMyAgencyReportsQuery;
using RemSolution.Application.Features.Agency.Commands.CreateAgencyBranchCommand;
using RemSolution.Application.Features.Agency.Commands.CreateAgencyCarCommand;
using RemSolution.Application.Features.Agency.Commands.CreateAgencyCommand;
using RemSolution.Application.Features.Agency.Commands.SetAgencyInvoiceSettingsCommand;
using RemSolution.Application.Features.Agency.Commands.DeleteAgencyBranchCommand;
using RemSolution.Application.Features.Agency.Commands.DeleteAgencyCommand;
using RemSolution.Application.Features.Agency.Commands.SetAgencyFeatureCommand;
using RemSolution.Application.Features.Agency.Commands.SetAgencyPublicationCommand;
using RemSolution.Application.Features.Agency.Commands.UpdateAgencyBranchCommand;
using RemSolution.Application.Features.Agency.Commands.UpdateAgencyCommand;
using RemSolution.Application.Features.Agency.Commands.UpdateMyAgencyCommand;
using RemSolution.Application.Features.Agency.DTOs;
using RemSolution.Application.Features.Agency.Queries.GetAgenciesQuery;
using RemSolution.Application.Features.Agency.Queries.GetAgencyBranchesQuery;
using RemSolution.Application.Features.Agency.Queries.GetAgencyByIdQuery;
using RemSolution.Application.Features.Agency.Queries.GetAgencyFeaturesQuery;
using RemSolution.Application.Features.Agency.Queries.GetAgencyPublicationQuery;
using RemSolution.Application.Features.Agency.Queries.GetMyAgencyQuery;
using RemSolution.Application.Features.Agency.Queries.GetMyAgencyReliabilityQuery;
using RemSolution.Application.Features.Branch.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Web.Endpoints;

public class Agencies : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        // "me" is mapped before "{id}" so it is never swallowed by the id route.
        // It is the one part of this group an agency administrator may reach: the
        // self-service view of their own agency, which takes no id at all.
        app.MapGroup(this)
            .RequireAuthorization(Policies.AgencyAdminOnly)
            .MapGet(GetMyAgency, "me")
            .MapPut(UpdateMyAgency, "me")
            // The public reliability figure, shown back to the agency it is
            // about, and the complaints behind it.
            .MapGet(GetMyAgencyReliability, "me/reliability")
            .MapGet(GetMyAgencyReports, "me/reports");

        app.MapGroup(this)
            .RequireAuthorization(Policies.PlatformAdminOnly)
            .MapGet(GetAgencies)
            .MapGet(GetAgencyById, "{id}")
            .MapPost(CreateAgency)
            .MapPut(UpdateAgency, "{id}")
            .MapDelete(DeleteAgency, "{id}")
            .MapGet(GetAgencyFeatures, "{id}/features")
            .MapPut(SetAgencyFeature, "{id}/features")
            // Going live on the marketplace, and what the agency has to show if
            // it does (see Agency.PublishedAt).
            .MapGet(GetAgencyPublication, "{id}/publication")
            .MapPut(SetAgencyPublication, "{id}/publication")
            // The two setup steps the agency cannot do for itself yet, because
            // nobody has signed in to it: its invoice settings and its first cars.
            .MapPut(SetAgencyInvoiceSettings, "{id}/invoice-settings")
            .MapPost(CreateAgencyCar, "{id}/cars")
            // An agency's locations, edited alongside the agency itself. The
            // agency's own administrator manages the same rows through the
            // Branches group, which takes its tenant from their claim.
            .MapGet(GetAgencyBranches, "{id}/branches")
            .MapPost(CreateAgencyBranch, "{id}/branches")
            .MapPut(UpdateAgencyBranch, "{id}/branches/{branchId}")
            .MapDelete(DeleteAgencyBranch, "{id}/branches/{branchId}");
    }

    public async Task<Ok<AgencyDto>> GetMyAgency(ISender sender)
    {
        var result = await sender.Send(new GetMyAgencyQuery());
        return TypedResults.Ok(result);
    }

    public async Task<NoContent> UpdateMyAgency(ISender sender, UpdateMyAgencyCommand command)
    {
        await sender.Send(command);
        return TypedResults.NoContent();
    }

    public async Task<Ok<AgencyReliabilityDto>> GetMyAgencyReliability(ISender sender)
    {
        var result = await sender.Send(new GetMyAgencyReliabilityQuery());
        return TypedResults.Ok(result);
    }

    public async Task<Ok<PaginatedList<AgencyReportDto>>> GetMyAgencyReports(
        ISender sender, [AsParameters] GetMyAgencyReportsQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Ok<IList<BranchDto>>> GetAgencyBranches(ISender sender, int id)
    {
        var result = await sender.Send(new GetAgencyBranchesQuery(id));
        return TypedResults.Ok(result);
    }

    public async Task<Results<Created<int>, BadRequest>> CreateAgencyBranch(ISender sender, int id, CreateAgencyBranchCommand command)
    {
        if (id != command.AgencyId)
            return TypedResults.BadRequest();

        var branchId = await sender.Send(command);

        return TypedResults.Created($"/api/Agencies/{id}/branches/{branchId}", branchId);
    }

    public async Task<Results<NoContent, BadRequest>> UpdateAgencyBranch(ISender sender, int id, int branchId, UpdateAgencyBranchCommand command)
    {
        if (id != command.AgencyId || branchId != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }

    public async Task<NoContent> DeleteAgencyBranch(ISender sender, int id, int branchId)
    {
        await sender.Send(new DeleteAgencyBranchCommand(id, branchId));
        return TypedResults.NoContent();
    }

    public async Task<Ok<IReadOnlyList<AgencyFeatureDto>>> GetAgencyFeatures(ISender sender, int id)
    {
        var result = await sender.Send(new GetAgencyFeaturesQuery(id));
        return TypedResults.Ok(result);
    }

    public async Task<Results<NoContent, BadRequest>> SetAgencyFeature(ISender sender, int id, SetAgencyFeatureCommand command)
    {
        if (id != command.AgencyId)
            return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }

    public async Task<Ok<AgencyPublicationDto>> GetAgencyPublication(ISender sender, int id)
    {
        var result = await sender.Send(new GetAgencyPublicationQuery(id));
        return TypedResults.Ok(result);
    }

    public async Task<Results<NoContent, BadRequest>> SetAgencyPublication(
        ISender sender, int id, SetAgencyPublicationCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }

    public async Task<Results<NoContent, BadRequest>> SetAgencyInvoiceSettings(
        ISender sender, int id, SetAgencyInvoiceSettingsCommand command)
    {
        if (id != command.AgencyId)
            return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }

    public async Task<Results<Created<int>, BadRequest>> CreateAgencyCar(
        ISender sender, int id, CreateAgencyCarCommand command)
    {
        if (id != command.AgencyId)
            return TypedResults.BadRequest();

        var carId = await sender.Send(command);

        return TypedResults.Created($"/api/Agencies/{id}/cars/{carId}", carId);
    }

    public async Task<Ok<IList<AgencyDto>>> GetAgencies(ISender sender, [AsParameters] GetAgenciesQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }

    public async Task<Results<Ok<AgencyDto>, NotFound>> GetAgencyById(ISender sender, int id)
    {
        var result = await sender.Send(new GetAgencyByIdQuery(id));

        if (result is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(result);
    }

    // Returns the outcome, not just the id: the caller needs the administrator
    // login and its one-time password.
    public async Task<Created<AgencyCreatedDto>> CreateAgency(ISender sender, CreateAgencyCommand command)
    {
        var result = await sender.Send(command);
        return TypedResults.Created($"/Agencies/{result.Id}", result);
    }

    public async Task<Results<NoContent, BadRequest>> UpdateAgency(ISender sender, int id, UpdateAgencyCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }

    public async Task<NoContent> DeleteAgency(ISender sender, int id)
    {
        await sender.Send(new DeleteAgencyCommand(id));
        return TypedResults.NoContent();
    }
}
