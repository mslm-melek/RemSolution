using RemSolution.Application.Features.Country.Commands.CreateCountryCommand;
using RemSolution.Application.Features.Country.Commands.UpdateCountryCommand;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Countries.Commands;

using static Testing;

public class UpdateCountryTests : BaseTestFixture
{
    [Test]
    public async Task ShouldUpdateCountry()
    {
        var countryId = await SendAsync(new CreateCountryCommand
        {
            Name = "Tunisi"
        });

        var command = new UpdateCountryCommand
        {
            Id = countryId,
            Name = "Tunisie"
        };

        await SendAsync(command);

        var country = await FindAsync<Country>(countryId);

        country!.Name.Should().Be("Tunisie");
    }
}
