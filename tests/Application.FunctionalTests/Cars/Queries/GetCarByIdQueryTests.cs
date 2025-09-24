using RemSolution.Application.Features.Car.Commands.CreateCarCommand;
using RemSolution.Application.Features.Car.Queries.GetCarByIdQuery;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Cars.Queries;

using static Testing;

public class GetCarByIdQueryTests : BaseTestFixture
{
    [Test]
    public async Task ShouldReturnCarById()
    {
        var brand = new Brand { Name = "Tesla" };
        await AddAsync(brand);

        var model = new ModelCar { Name = "Model S", BrandId = brand.Id };
        await AddAsync(model);

        var carId = await SendAsync(new CreateCarCommand
        {
            Matricule = "XYZ-789",
            ModelId = model.Id,
            Color = "Blue",
            FirstCirculationDate = DateTime.UtcNow
        });

        var result = await SendAsync(new GetCarByIdQuery(carId));

        result.Should().NotBeNull();
        result!.Matricule.Should().Be("XYZ-789");
    }
}
