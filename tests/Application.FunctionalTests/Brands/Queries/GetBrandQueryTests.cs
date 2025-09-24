using RemSolution.Application.Features.Brand.Commands.CreateBrandCommand;
using RemSolution.Application.Features.Brand.Queries.GetBrandsQuery;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Brands.Queries;

using static Testing;

public class GetBrandsWithPaginationQueryTests : BaseTestFixture
{
    [Test]
    public async Task ShouldReturnPaginatedBrands()
    {
        await SendAsync(new CreateBrandCommand { Name = "BMX" });
        await SendAsync(new CreateBrandCommand { Name = "TOYOTA" });

        var query = new GetBrandsQuery();

        var result = await SendAsync(query);

        result.Count().Should().BeGreaterThan(1);
    }
}
