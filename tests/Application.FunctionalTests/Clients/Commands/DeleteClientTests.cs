using RemSolution.Application.Features.Client.Commands.CreateClientCommand;
using RemSolution.Application.Features.Client.Commands.DeleteClientCommand;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Clients.Commands;

using static Testing;

public class DeleteClientTests : BaseTestFixture
{
    [Test]
    public async Task ShouldArchiveClientAndHideItFromReads()
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

        // Hidden from normal (filtered) reads...
        (await CountAsync<Client>(c => c.Id == clientId)).Should().Be(0);

        // ...but the row survives, flagged, with who/when stamped.
        var archived = await FindIgnoringFiltersAsync<Client>(c => c.Id == clientId);
        archived.Should().NotBeNull();
        archived!.IsDeleted.Should().BeTrue();
        archived.DeletedAt.Should().NotBeNull();
        archived.DeletedBy.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task ArchivingAClientPreservesItsFinancialRecords()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var clientId = await SendAsync(new CreateClientCommand
        {
            FirstName = "Jane",
            LastName = "Roe",
            BirthDate = new DateTime(1988, 1, 1)
        });

        // A payment (financial record) referencing the client.
        var payment = new Payment { ClientId = clientId };
        await AddAsync(payment);

        await SendAsync(new DeleteClientCommand(clientId));

        // The payment survives and still references the (archived) client.
        var preserved = await FindAsync<Payment>(payment.Id);
        preserved.Should().NotBeNull();
        preserved!.ClientId.Should().Be(clientId);

        (await FindIgnoringFiltersAsync<Client>(c => c.Id == clientId))!.IsDeleted.Should().BeTrue();
    }
}
