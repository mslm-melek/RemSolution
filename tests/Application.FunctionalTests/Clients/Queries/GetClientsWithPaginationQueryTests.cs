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
}
