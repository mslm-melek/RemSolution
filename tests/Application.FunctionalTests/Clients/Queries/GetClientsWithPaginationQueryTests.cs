using RemSolution.Application.Features.Client.Commands.CreateClientCommand;
using RemSolution.Application.Features.Client.Queries.GetClientsWithPaginationQuery;

namespace RemSolution.Application.FunctionalTests.Clients.Queries;

using static Testing;

public class GetClientsWithPaginationQueryTests : BaseTestFixture
{
    [Test]
    public async Task ShouldReturnPaginatedClients()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await SendAsync(new CreateClientCommand { FirstName = "John", LastName = "Doe", BirthDate = new DateTime(1990, 5, 20) });
        await SendAsync(new CreateClientCommand { FirstName = "Jane", LastName = "Smith", BirthDate = new DateTime(1985, 3, 10) });

        var query = new GetClientsWithPaginationQuery { PageNumber = 1, PageSize = 10 };

        var result = await SendAsync(query);

        result.Items.Should().HaveCountGreaterThan(0);
        result.TotalCount.Should().BeGreaterThan(1);
    }

    [Test]
    public async Task ShouldFilterBySearch()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await SendAsync(new CreateClientCommand { FirstName = "John", LastName = "Doe", BirthDate = new DateTime(1990, 5, 20) });
        await SendAsync(new CreateClientCommand { FirstName = "Jane", LastName = "Smith", BirthDate = new DateTime(1985, 3, 10) });

        var result = await SendAsync(new GetClientsWithPaginationQuery { Search = "Smith" });

        result.TotalCount.Should().Be(1);
        result.Items.First().LastName.Should().Be("Smith");
    }

    // The dashboard's "flagged clients" alert links into this list, so the filter
    // has to select exactly the clients it counted.
    [Test]
    public async Task ShouldFilterFlaggedClients()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await AddAsync(new Domain.Entities.Client { FirstName = "Trouble", LastName = "Maker", IsFlagged = true });
        await AddAsync(new Domain.Entities.Client { FirstName = "Regular", LastName = "Customer" });

        var flagged = await SendAsync(new GetClientsWithPaginationQuery { Flagged = true });

        flagged.TotalCount.Should().Be(1);
        flagged.Items.First().LastName.Should().Be("Maker");

        var rest = await SendAsync(new GetClientsWithPaginationQuery { Flagged = false });

        rest.TotalCount.Should().Be(1);
        rest.Items.First().LastName.Should().Be("Customer");
    }
}
