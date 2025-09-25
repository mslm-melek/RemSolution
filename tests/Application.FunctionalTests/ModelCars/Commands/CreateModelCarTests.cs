using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Car.Commands.CreateCarCommand;
using RemSolution.Application.Features.ModelCar.Commands.CreateModelCarCommand;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.ModelCars.Commands;

using static Testing;

public class CreateModelCarTests : BaseTestFixture
{
    [Test]
    public async Task ShouldRequireMinimumFields()
    {
        var command = new CreateCarCommand(); // empty

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldCreateModelCar()
    {
        var userId = await RunAsDefaultUserAsync();

        var brand = new Brand { Name = "Tesla" };
        await AddAsync(brand);

        var command = new CreateModelCarCommand
        {
            Name = "Model1",
            BrandId = brand.Id
        };

        var modelCarId = await SendAsync(command);

        var modelCar = await FindAsync<ModelCar>(modelCarId);

        modelCar.Should().NotBeNull();
        modelCar!.Name.Should().Be("Model1");
        modelCar.BrandId.Should().Be(brand.Id);
        modelCar.CreatedBy.Should().Be(userId);
    }
}
