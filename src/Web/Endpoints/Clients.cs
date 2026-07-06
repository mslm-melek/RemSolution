using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Features.Client.Commands.CreateClientCommand;

namespace RemSolution.Web.Endpoints;

public class Clients : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .RequireAuthorization()
            .MapPost(CreateClient);
    }

    public async Task<Created<int>> CreateClient(ISender sender, CreateClientCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/clients/{id}", id);
    }
}
