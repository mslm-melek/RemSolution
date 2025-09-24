using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Car.Commands.CreateCarCommand;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.FunctionalTests.Cars.Commands;

using static Testing;

public class CreateCarTests : BaseTestFixture
{
    [Test]
    public async Task ShouldRequireMinimumFields()
    {
        var command = new CreateCarCommand(); // empty

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldCreateCar()
    {
        var userId = await RunAsDefaultUserAsync();

        var brand = new Brand { Name = "Tesla" };
        await AddAsync(brand);

        var model = new ModelCar { Name = "Model 3", BrandId = brand.Id };
        await AddAsync(model);

        var command = new CreateCarCommand
        {
            Matricule = "ABC-123",
            ModelId = model.Id,
            Color = "Red",
            FirstCirculationDate = DateTime.UtcNow,
            Power = 120,
            FuelType = FuelType.Gasoline
        };

        var carId = await SendAsync(command);

        var car = await FindAsync<Car>(carId);

        car.Should().NotBeNull();
        car!.Matricule.Should().Be("ABC-123");
        car.ModelId.Should().Be(model.Id);
        car.CreatedBy.Should().Be(userId);
    }
}
