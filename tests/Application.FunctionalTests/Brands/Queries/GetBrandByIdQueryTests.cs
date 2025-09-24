using RemSolution.Application.Features.Brand.Commands.CreateBrandCommand;
using RemSolution.Application.Features.Brand.Queries.GetBrandByIdQuery;

namespace RemSolution.Application.FunctionalTests.Brands.Queries;

using static Testing;

public class GetBrandByIdQueryTests : BaseTestFixture
{
    [Test]
    public async Task ShouldReturnBrandById()
    {
        var brandId = await SendAsync(new CreateBrandCommand
        {
            Name = "BMW"
        });

        var result = await SendAsync(new GetBrandByIdQuery(brandId));

        result.Should().NotBeNull();
        result!.Name.Should().Be("BMW");
    }
}
