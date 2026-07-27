using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Domain.Constants;
using RemSolution.Application.Features.Contract.Commands.GenerateContractCommand;
using RemSolution.Application.Features.Contract.DTOs;
using RemSolution.Application.Features.Contract.Queries.GetContractDocumentQuery;
using RemSolution.Application.Features.Contract.Queries.GetContractsByRentingQuery;

namespace RemSolution.Web.Endpoints;

public class Contracts : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app.MapGroup(this)
            .RequireAuthorization();

        group
            .MapGet(GetContractsByRenting, "renting/{rentingId}", Permissions.ContractRead)
            .MapGet(DownloadContract, "{id}/download", Permissions.ContractRead)
            .MapPost(GenerateContract, "renting/{rentingId}", Permissions.ContractGenerate);
    }

    public async Task<Ok<IList<ContractDto>>> GetContractsByRenting(ISender sender, int rentingId)
    {
        var result = await sender.Send(new GetContractsByRentingQuery(rentingId));
        return TypedResults.Ok(result);
    }

    public async Task<Results<FileStreamHttpResult, NotFound>> DownloadContract(ISender sender, int id)
    {
        var download = await sender.Send(new GetContractDocumentQuery(id));

        if (download is null)
            return TypedResults.NotFound();

        // Named download (Content-Disposition: attachment) so the saved file
        // carries the contract number rather than the route's "download".
        return TypedResults.File(download.Content, download.ContentType, download.FileName);
    }

    // The body carries the template choice and the values the agent was prompted
    // for; an empty body means "the default template, nothing to fill in".
    public async Task<Created<ContractDto>> GenerateContract(
        ISender sender, int rentingId, GenerateContractCommand? command)
    {
        var contract = await sender.Send((command ?? new GenerateContractCommand()) with
        {
            // The route owns the renting id; the body cannot disagree with it.
            RentingId = rentingId
        });

        return TypedResults.Created($"/contracts/{contract.Id}/download", contract);
    }
}
