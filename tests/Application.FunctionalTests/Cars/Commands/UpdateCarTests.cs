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
        await RunAsAgencyAdministratorAsync();
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

    // The car screen is where a wrong odometer gets fixed, so unlike a reading
    // taken off a hire this one is assigned as given — downwards included.
    [Test]
    public async Task ShouldSetTheOdometerInEitherDirection()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var brand = new Brand { Name = "Kia" };
        await AddAsync(brand);

        var model = new ModelCar { Name = "Ceed", BrandId = brand.Id };
        await AddAsync(model);

        var carId = await SendAsync(new CreateCarCommand
        {
            Matricule = "ODO-EDIT",
            ModelId = model.Id,
            FirstCirculationDate = DateTime.UtcNow,
            Mileage = 61_000
        });

        (await FindAsync<Car>(carId))!.Mileage.Should().Be(61_000);

        // A digit too many was typed when the car was added.
        await SendAsync(new UpdateCarCommand { Id = carId, Mileage = 6_100 });

        (await FindAsync<Car>(carId))!.Mileage.Should().Be(6_100);
    }
}
