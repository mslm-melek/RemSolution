using RemSolution.Application.Features.ModelCar.Commands.CreateModelCarCommand;
using RemSolution.Application.Features.ModelCar.Queries.GetModelCarByIdQuery;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.ModelCars.Queries;

using static Testing;

public class GetModelCarByIdQueryTests : BaseTestFixture
{
    [Test]
    public async Task ShouldReturnModelCarById()
    {
        var brand = new Brand { Name = "Tesla" };
        await AddAsync(brand);

        var modelCarId = await SendAsync(new CreateModelCarCommand
        {
            Name = "Model1",
            BrandId = brand.Id
        });


        var result = await SendAsync(new GetModelCarByIdQuery(modelCarId));

        result.Should().NotBeNull();
        result!.Name.Should().Be("Model1");
    }
}
