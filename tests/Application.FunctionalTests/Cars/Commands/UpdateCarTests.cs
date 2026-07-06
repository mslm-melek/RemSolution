using RemSolution.Application.Features.Car.Commands.CreateCarCommand;
using RemSolution.Application.Features.Car.Commands.UpdateCarCommand;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Cars.Commands;

using static Testing;

public class UpdateCarTests : BaseTestFixture
{
    [Test]
    public async Task ShouldUpdateCar()

    {
        await AddTestAgencyAsync();

        var brand = new Brand { Name = "Tesla" };
        await AddAsync(brand);

        var model = new ModelCar { Name = "Model S", BrandId = brand.Id };
        await AddAsync(model);

        var carId = await SendAsync(new CreateCarCommand
        {
            Matricule = "OLD-111",
            ModelId = model.Id,
            Color = "Black",
            FirstCirculationDate = DateTime.UtcNow
        });

        var command = new UpdateCarCommand
        {
            Id = carId,
            Color = "Blue"
        };

        await SendAsync(command);

        var car = await FindAsync<Car>(carId);

        car!.Color.Should().Be("Blue");
    }
}
