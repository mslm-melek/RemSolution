using RemSolution.Application.Features.Car.Commands.CreateCarCommand;
using RemSolution.Application.Features.Car.Commands.DeleteCarCommand;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Cars.Commands;

using static Testing;

public class DeleteCarTests : BaseTestFixture
{
    [Test]
    public async Task ShouldDeleteCar()
    {
        await AddTestAgencyAsync();

        var brand = new Brand { Name = "Tesla" };
        await AddAsync(brand);

        var model = new ModelCar { Name = "Model S", BrandId = brand.Id };
        await AddAsync(model);
        var carId = await SendAsync(new CreateCarCommand
        {
            Matricule = "DEL-999",
            ModelId = model.Id,
            Color = "Gray",
            FirstCirculationDate = DateTime.UtcNow
        });

        await SendAsync(new DeleteCarCommand(carId));

        var car = await FindAsync<Car>(carId);

        car.Should().BeNull();
    }
}
