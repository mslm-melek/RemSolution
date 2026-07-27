using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Domain.Constants;
using RemSolution.Application.Features.Facture.Commands.GenerateFactureCommand;
using RemSolution.Application.Features.Facture.DTOs;
using RemSolution.Application.Features.Facture.Queries.GetFactureDocumentQuery;
using RemSolution.Application.Features.Facture.Queries.GetFacturesByRentingQuery;

namespace RemSolution.Web.Endpoints;

public class Factures : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this)
            .RequireAuthorization();

        group
            .MapGet(GetFacturesByRenting, "renting/{rentingId}", Permissions.FactureRead)
            .MapGet(DownloadFacture, "{id}/download", Permissions.FactureRead)
            .MapPost(GenerateFacture, "renting/{rentingId}", Permissions.FactureGenerate);
    }

    public async Task<Ok<IList<FactureDto>>> GetFacturesByRenting(ISender sender, int rentingId)
    {
        var result = await sender.Send(new GetFacturesByRentingQuery(rentingId));
        return TypedResults.Ok(result);
    }

    public async Task<Results<FileStreamHttpResult, NotFound>> DownloadFacture(ISender sender, int id)
    {
        var download = await sender.Send(new GetFactureDocumentQuery(id));

        if (download is null)
            return TypedResults.NotFound();

        return TypedResults.File(download.Content, download.ContentType, download.FileName);
    }

    // See Contracts.GenerateContract for why the renting id comes from the route.
    public async Task<Created<FactureDto>> GenerateFacture(
        ISender sender, int rentingId, GenerateFactureCommand? command)
    {
        var facture = await sender.Send((command ?? new GenerateFactureCommand()) with
        {
            RentingId = rentingId
        });

        return TypedResults.Created($"/factures/{facture.Id}/download", facture);
    }
}
