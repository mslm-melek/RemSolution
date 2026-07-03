using RemSolution.Application.Features.ModelCar.Commands.CreateModelCarCommand;
using RemSolution.Application.Features.ModelCar.Commands.UpdateModelCarCommand;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.ModelCars.Commands;

using static Testing;

public class UpdateModelCarTests : BaseTestFixture
{
    [Test]
    public async Task ShouldUpdateModelCar()
    {
        var brand = new Brand { Name = "Tesla" };
        await AddAsync(brand);

        var modelId = await SendAsync(new CreateModelCarCommand
        {
            Name = "Model S",
            BrandId = brand.Id
        });

        var command = new UpdateModelCarCommand
        {
            Id = modelId,
            Name = "Model X",
            BrandId = brand.Id
        };

        await SendAsync(command);

        var model = await FindAsync<ModelCar>(modelId);

        model!.Name.Should().Be("Model X");
    }
}
