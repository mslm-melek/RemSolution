using RemSolution.Application.Features.ModelCar.Commands.CreateModelCarCommand;
using RemSolution.Application.Features.ModelCar.Queries.GetModelCarsWithPaginationQuery;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.ModelCars.Queries;

using static Testing;

public class GetModelCarsWithPaginationQueryTests : BaseTestFixture
{
    [Test]
    public async Task ShouldReturnPaginatedCars()
    {
        var brand = new Brand { Name = "Tesla" };
        await AddAsync(brand);

        await SendAsync(new CreateModelCarCommand { Name = "Model S", BrandId = brand.Id });
        await SendAsync(new CreateModelCarCommand { Name = "Model X", BrandId = brand.Id });

        var query = new GetModelCarsWithPaginationQuery { PageNumber = 1, PageSize = 10 };

        var result = await SendAsync(query);

        result.Items.Should().HaveCountGreaterThan(0);
        result.TotalCount.Should().BeGreaterThan(1);
    }
}
