using RemSolution.Application.Features.Client.Commands.CreateClientCommand;
using RemSolution.Application.Features.Client.Queries.GetClientsWithPaginationQuery;
using RemSolution.Domain.Entities;
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

    // The book's search box is one field over the three things a counter has in
    // front of them: the name they were told, the address the booking came from,
    // or the card they are holding — half-read, which is why it matches partially.
    [Test]
    public async Task ShouldFindClientsByEmailOrCin()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await AddAsync(new Domain.Entities.Client
        {
            FirstName = "Ahmed", LastName = "BenSalem",
            Email = "ahmed.ben@example.com", CIN = "09876543"
        });
        await AddAsync(new Domain.Entities.Client
        {
            FirstName = "Fatima", LastName = "Khoury",
            Email = "fatima.kh@example.com", CIN = "12345678"
        });

        var byEmail = await SendAsync(new GetClientsWithPaginationQuery { Search = "fatima.kh@" });

        byEmail.TotalCount.Should().Be(1);
        byEmail.Items.First().LastName.Should().Be("Khoury");

        var byCin = await SendAsync(new GetClientsWithPaginationQuery { Search = "098765" });

        byCin.TotalCount.Should().Be(1);
        byCin.Items.First().LastName.Should().Be("BenSalem");
    }

    // The book's "papers missing" filter: what has to be produced if the car is
    // stopped is the CIN image and the licence, so a passport on its own is not
    // a complete file (see the client-standing note in the SPA).
    [Test]
    public async Task ShouldFilterOnPapersOnFile()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var cinFile = await StoreFileAsync(DocumentType.CIN);
        var licenceFile = await StoreFileAsync(DocumentType.DrivingLicence);
        var passeportFile = await StoreFileAsync(DocumentType.Passeport);

        await AddAsync(new Domain.Entities.Client
        {
            FirstName = "Papers", LastName = "Complete",
            CINFileId = cinFile.Id, DrivingLicenceFileId = licenceFile.Id
        });
        await AddAsync(new Domain.Entities.Client
        {
            FirstName = "Licence", LastName = "Missing",
            CINFileId = cinFile.Id, PasseportFileId = passeportFile.Id
        });
        await AddAsync(new Domain.Entities.Client { FirstName = "Nothing", LastName = "OnFile" });

        var complete = await SendAsync(new GetClientsWithPaginationQuery { DocumentsComplete = true });

        complete.TotalCount.Should().Be(1);
        complete.Items.First().LastName.Should().Be("Complete");

        var incomplete = await SendAsync(new GetClientsWithPaginationQuery { DocumentsComplete = false });

        incomplete.TotalCount.Should().Be(2);
        incomplete.Items.Select(c => c.LastName).Should().BeEquivalentTo(new[] { "Missing", "OnFile" });
    }

    // A stored document, without going through the upload command: this asks what
    // the query does with the FK, not how the file got there.
    private static async Task<StoredFile> StoreFileAsync(DocumentType type)
    {
        var file = new StoredFile
        {
            Path = $"clients/{type}.png",
            Url = $"/uploads/clients/{type}.png",
            OriginalFileName = $"{type}.png",
            MimeType = "image/png",
            Size = 8,
            Sha256 = $"{type}-hash",
            DocumentType = type
        };

        await AddAsync(file);

        return file;
    }
}
