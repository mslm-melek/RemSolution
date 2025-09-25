using RemSolution.Application.Features.ModelCar.Commands.CreateModelCarCommand;
using RemSolution.Application.Features.ModelCar.Commands.DeleteModelCarCommand;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.ModelCars.Commands;

using static Testing;

public class DeleteModelCarTests : BaseTestFixture
{
    [Test]
    public async Task ShouldDeleteCar()
    {
        var brand = new Brand { Name = "Tesla" };
        await AddAsync(brand);

        var modelCarId = await SendAsync(new CreateModelCarCommand
        {
            Name = "Model1",
            BrandId = brand.Id
        });



        await SendAsync(new DeleteModelCarCommand(modelCarId));

        var car = await FindAsync<Car>(modelCarId);

        car.Should().BeNull();
    }
}
