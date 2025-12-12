using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Features.Country.Commands.CreateCountryCommand;
using RemSolution.Application.Features.Country.Commands.DeleteCountryCommand;
using RemSolution.Application.Features.Country.DTOs;
using RemSolution.Application.Features.Country.Queries.GetCountryByIdQuery;
using RemSolution.Application.Features.Country.Queries.GetCountriesQuery;

namespace RemSolution.Web.Endpoints;

public class Countries : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .RequireAuthorization()
            .MapGet(GetCountries)        
            .MapGet(GetCountryById, "{id}")
            .MapPost(CreateCountry)
            .MapDelete(DeleteCountry, "{id}");
    }

    public async Task<Ok<IList<CountryDto>>> GetCountries(ISender sender, [AsParameters] GetCountriesQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }
  
    public async Task<Results<Ok<CountryDto>, NotFound>> GetCountryById(ISender sender, int id)
    {
        var result = await sender.Send(new GetCountryByIdQuery(id));

        if (result is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(result);
    }

    public async Task<Created<int>> CreateCountry(ISender sender, CreateCountryCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/Countries/{id}", id);
    }
    public async Task<NoContent> DeleteCountry(ISender sender, int id)
    {
        await sender.Send(new DeleteCountryCommand(id));
        return TypedResults.NoContent();
    }

}
