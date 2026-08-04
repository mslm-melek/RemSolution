using RemSolution.Application.Features.Client.Commands.CreateClientCommand;
using RemSolution.Application.Features.Client.Queries.GetClientsWithPaginationQuery;
using RemSolution.Domain.Enums;

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

    // The client list shows how much history each name has, and links to it.
    [Test]
    public async Task ShouldCountTheClientsHires()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var client = new Domain.Entities.Client { FirstName = "Repeat", LastName = "Renter" };
        await AddAsync(client);
        var passenger = new Domain.Entities.Client { FirstName = "Second", LastName = "Driver" };
        await AddAsync(passenger);
        await AddAsync(new Domain.Entities.Client { FirstName = "First", LastName = "Timer" });

        var car = new Domain.Entities.Car { Matricule = "HIST-1", Status = CarStatus.Active };
        await AddAsync(car);

        await AddAsync(new Domain.Entities.Renting
        {
            CarId = car.Id, ClientId = client.Id, SecondClientId = passenger.Id,
            StartDate = DateTime.UtcNow.AddDays(-20), EndDate = DateTime.UtcNow.AddDays(-18),
            RentingState = RentingState.Done
        });
        await AddAsync(new Domain.Entities.Renting
        {
            CarId = car.Id, ClientId = client.Id,
            StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(2),
            RentingState = RentingState.InProgress
        });
        // Cancelled hires are not history.
        await AddAsync(new Domain.Entities.Renting
        {
            CarId = car.Id, ClientId = client.Id,
            StartDate = DateTime.UtcNow.AddDays(5), EndDate = DateTime.UtcNow.AddDays(6),
            RentingState = RentingState.Cancelled
        });

        var result = await SendAsync(new GetClientsWithPaginationQuery());

        var renter = result.Items.Single(c => c.LastName == "Renter");
        renter.RentingCount.Should().Be(2);
        renter.OpenRentingCount.Should().Be(1);

        // Being someone else's second driver is part of a client's history too —
        // it is what the count's link shows.
        var passengerRow = result.Items.Single(c => c.LastName == "Driver");
        passengerRow.RentingCount.Should().Be(1);
        passengerRow.OpenRentingCount.Should().Be(0);

        var newcomer = result.Items.Single(c => c.LastName == "Timer");
        newcomer.RentingCount.Should().Be(0);
        newcomer.OpenRentingCount.Should().Be(0);
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
