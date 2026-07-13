using RemSolution.Application.Features.Client.Commands.CreateClientCommand;
using RemSolution.Application.Features.Client.Commands.UpdateClientCommand;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Clients.Commands;

using static Testing;

public class UpdateClientTests : BaseTestFixture
{
    [Test]
    public async Task ShouldUpdateClient()
    {
        await AddTestAgencyAsync();

        var clientId = await SendAsync(new CreateClientCommand
        {
            FirstName = "John",
            LastName = "Doe",
            BirthDate = new DateTime(1990, 5, 20)
        });

        var command = new UpdateClientCommand
        {
            Id = clientId,
            FirstName = "Jane",
            LastName = "Doe",
            BirthDate = new DateTime(1990, 5, 20),
            Description = "Updated"
        };

        await SendAsync(command);

        var client = await FindAsync<Client>(clientId);

        client!.FirstName.Should().Be("Jane");
        client.Description.Should().Be("Updated");
    }
}
