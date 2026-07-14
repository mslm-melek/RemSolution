using RemSolution.Application.Features.Client.Commands.CreateClientCommand;
using RemSolution.Application.Features.Client.Commands.DeleteClientCommand;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Clients.Commands;

using static Testing;

public class DeleteClientTests : BaseTestFixture
{
    [Test]
    public async Task ShouldDeleteClient()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var clientId = await SendAsync(new CreateClientCommand
        {
            FirstName = "John",
            LastName = "Doe",
            BirthDate = new DateTime(1990, 5, 20)
        });

        await SendAsync(new DeleteClientCommand(clientId));

        var client = await FindAsync<Client>(clientId);

        client.Should().BeNull();
    }

    [Test]
    public async Task ShouldClearSecondDriverReferencesWhenDeleting()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var clientId = await SendAsync(new CreateClientCommand
        {
            FirstName = "Second",
            LastName = "Driver",
            BirthDate = new DateTime(1985, 3, 10)
        });

        // Renting.SecondClientId is a NO ACTION FK; deleting the client must
        // clear the reference instead of failing with an FK violation.
        var renting = new Renting { SecondClientId = clientId };
        await AddAsync(renting);

        await SendAsync(new DeleteClientCommand(clientId));

        (await FindAsync<Client>(clientId)).Should().BeNull();

        var updatedRenting = await FindAsync<Renting>(renting.Id);
        updatedRenting.Should().NotBeNull();
        updatedRenting!.SecondClientId.Should().BeNull();
    }
}
