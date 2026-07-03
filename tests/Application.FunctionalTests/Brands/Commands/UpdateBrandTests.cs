using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Brand.Commands.CreateBrandCommand;
using RemSolution.Application.Features.Brand.Commands.UpdateBrandCommand;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Brands.Commands;

using static Testing;

public class UpdateBrandTests : BaseTestFixture
{
    [Test]
    public async Task ShouldUpdateBrand()
    {
        var brandId = await SendAsync(new CreateBrandCommand
        {
            Name = "Toyota"
        });

        var command = new UpdateBrandCommand
        {
            Id = brandId,
            Name = "Lexus"
        };

        await SendAsync(command);

        var brand = await FindAsync<Brand>(brandId);

        brand!.Name.Should().Be("Lexus");
    }

    [Test]
    public async Task ShouldRequireUniqueName()
    {
        await SendAsync(new CreateBrandCommand
        {
            Name = "Toyota"
        });

        var brandId = await SendAsync(new CreateBrandCommand
        {
            Name = "Honda"
        });

        var command = new UpdateBrandCommand
        {
            Id = brandId,
            Name = "Toyota"
        };

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }
}
