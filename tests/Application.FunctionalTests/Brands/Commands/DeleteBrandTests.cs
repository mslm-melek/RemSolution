using RemSolution.Application.Features.Brand.Commands.CreateBrandCommand;
using RemSolution.Application.Features.Brand.Commands.DeleteBrandCommand;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Brands.Commands;

using static Testing;

public class DeleteBrandTests : BaseTestFixture
{
    [Test]
    public async Task ShouldDeleteBrand()
    {
       
        var brandId = await SendAsync(new CreateBrandCommand
        {
           Name = "BMW"
        });

        await SendAsync(new DeleteBrandCommand(brandId));

        var brand = await FindAsync<Brand>(brandId);

        brand.Should().BeNull();
    }
}
