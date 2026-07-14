using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Features.Branch.Commands.CreateBranchCommand;
using RemSolution.Application.Features.Branch.Commands.DeleteBranchCommand;
using RemSolution.Application.Features.Branch.Commands.UpdateBranchCommand;
using RemSolution.Application.Features.Branch.DTOs;
using RemSolution.Application.Features.Branch.Queries.GetBranchByIdQuery;
using RemSolution.Application.Features.Branch.Queries.GetBranchesQuery;
using RemSolution.Domain.Constants;

namespace RemSolution.Web.Endpoints;

public class Branches : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        // Branch management is an agency-administrator concern; the commands
        // and queries carry the same [Authorize] role for defence in depth.
        app.MapGroup(this)
            .RequireAuthorization(Policies.AgencyAdminOnly)
            .MapGet(GetBranches)
            .MapGet(GetBranchById, "{id}")
            .MapPost(CreateBranch)
            .MapPut(UpdateBranch, "{id}")
            .MapDelete(DeleteBranch, "{id}");
    }

    public async Task<Ok<IList<BranchDto>>> GetBranches(ISender sender)
    {
        var result = await sender.Send(new GetBranchesQuery());
        return TypedResults.Ok(result);
    }

    public async Task<Ok<BranchDto>> GetBranchById(ISender sender, int id)
    {
        // A missing id surfaces as NotFoundException from the handler; the
        // exception handler turns it into the 404 response.
        var result = await sender.Send(new GetBranchByIdQuery(id));

        return TypedResults.Ok(result);
    }

    public async Task<Created<int>> CreateBranch(ISender sender, CreateBranchCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/branches/{id}", id);
    }

    public async Task<Results<NoContent, BadRequest>> UpdateBranch(ISender sender, int id, UpdateBranchCommand command)
    {
        if (id != command.Id)
            return TypedResults.BadRequest();

        await sender.Send(command);

        return TypedResults.NoContent();
    }

    public async Task<NoContent> DeleteBranch(ISender sender, int id)
    {
        await sender.Send(new DeleteBranchCommand(id));
        return TypedResults.NoContent();
    }
}
