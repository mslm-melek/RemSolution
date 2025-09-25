using Microsoft.AspNetCore.Http.HttpResults;
using RemSolution.Application.Features.Brand.Commands.CreateBrandCommand;
using RemSolution.Application.Features.Brand.Commands.DeleteBrandCommand;
using RemSolution.Application.Features.Brand.DTOs;
using RemSolution.Application.Features.Brand.Queries.GetBrandByIdQuery;
using RemSolution.Application.Features.Brand.Queries.GetBrandsQuery;

namespace RemSolution.Web.Endpoints;

public class Brands : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        app.MapGroup(this)
            .RequireAuthorization()
            .MapGet(GetBrands)        
            .MapGet(GetBrandById, "{id}")
            .MapPost(CreateBrand)
            .MapDelete(DeleteBrand, "{id}");
    }

    public async Task<Ok<IList<BrandDto>>> GetBrands(ISender sender, [AsParameters] GetBrandsQuery query)
    {
        var result = await sender.Send(query);
        return TypedResults.Ok(result);
    }
  
    public async Task<Results<Ok<BrandDto>, NotFound>> GetBrandById(ISender sender, int id)
    {
        var result = await sender.Send(new GetBrandByIdQuery(id));

        if (result is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(result);
    }

    public async Task<Created<int>> CreateBrand(ISender sender, CreateBrandCommand command)
    {
        var id = await sender.Send(command);
        return TypedResults.Created($"/Brands/{id}", id);
    }
    public async Task<NoContent> DeleteBrand(ISender sender, int id)
    {
        await sender.Send(new DeleteBrandCommand(id));
        return TypedResults.NoContent();
    }

}
