using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Brand.Commands.CreateBrandCommand;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Brands.Commands;

using static Testing;

public class CreateBrandTests : BaseTestFixture
{
    [Test]
    public async Task ShouldRequireMinimumFields()
    {
        var command = new CreateBrandCommand(); // empty

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldCreateBrand()
    {
        var userId = await RunAsDefaultUserAsync();

        var command = new CreateBrandCommand
        {
            Name = "Toyota"
        };

        var brandId = await SendAsync(command);

        var brand = await FindAsync<Brand>(brandId);

        brand.Should().NotBeNull();
        brand!.Name.Should().Be("Toyota");
        brand.CreatedBy.Should().Be(userId);
    }
}
