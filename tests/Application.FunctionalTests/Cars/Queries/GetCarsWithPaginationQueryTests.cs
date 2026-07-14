using RemSolution.Application.Features.Car.Commands.CreateCarCommand;
using RemSolution.Application.Features.Car.Queries.GetCarsWithPaginationQuery;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Cars.Queries;

using static Testing;

public class GetCarsWithPaginationQueryTests : BaseTestFixture
{
    [Test]
    public async Task ShouldReturnPaginatedCars()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var brand = new Brand { Name = "Tesla" };
        await AddAsync(brand);

        var model = new ModelCar { Name = "Model S", BrandId = brand.Id };
        await AddAsync(model);
        await SendAsync(new CreateCarCommand { Matricule = "CAR-1", ModelId = model.Id, Color = "Black", FirstCirculationDate = DateTime.UtcNow });
        await SendAsync(new CreateCarCommand { Matricule = "CAR-2", ModelId = model.Id, Color = "White", FirstCirculationDate = DateTime.UtcNow });

        var query = new GetCarsWithPaginationQuery { PageNumber = 1, PageSize = 10 };

        var result = await SendAsync(query);

        result.Items.Should().HaveCountGreaterThan(0);
        result.TotalCount.Should().BeGreaterThan(1);
    }
}
