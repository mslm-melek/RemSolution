using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Client.Commands.CreateClientCommand;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Clients.Commands;

using static Testing;

public class CreateClientTests : BaseTestFixture
{
    [Test]
    public async Task ShouldRequireMinimumFields()
    {
        await RunAsAgencyAdministratorAsync();

        var command = new CreateClientCommand(); // empty

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldRequireBirthDate()
    {
        await RunAsAgencyAdministratorAsync();

        var command = new CreateClientCommand
        {
            FirstName = "John",
            LastName = "Doe"
        };

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldRejectBirthDateInTheFuture()
    {
        await RunAsAgencyAdministratorAsync();

        var command = new CreateClientCommand
        {
            FirstName = "John",
            LastName = "Doe",
            BirthDate = DateTime.UtcNow.AddYears(1)
        };

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldRejectInvalidCinFormat()
    {
        await RunAsAgencyAdministratorAsync();

        var command = new CreateClientCommand
        {
            FirstName = "John",
            LastName = "Doe",
            BirthDate = new DateTime(1990, 5, 20),
            CIN = "A!" // too short and invalid character
        };

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldRejectIssueDateInTheFuture()
    {
        await RunAsAgencyAdministratorAsync();

        var command = new CreateClientCommand
        {
            FirstName = "John",
            LastName = "Doe",
            BirthDate = new DateTime(1990, 5, 20),
            CIN = "AB123456",
            CINDeliveranceDate = DateTime.UtcNow.AddDays(2)
        };

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldRejectIssueDateBeforeBirthDate()
    {
        await RunAsAgencyAdministratorAsync();

        var command = new CreateClientCommand
        {
            FirstName = "John",
            LastName = "Doe",
            BirthDate = new DateTime(1990, 5, 20),
            PasseportNumber = "P1234567",
            PasseportDeliveranceDate = new DateTime(1980, 1, 1)
        };

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldRequireNumberWhenIssueDetailsProvided()
    {
        await RunAsAgencyAdministratorAsync();

        var command = new CreateClientCommand
        {
            FirstName = "John",
            LastName = "Doe",
            BirthDate = new DateTime(1990, 5, 20),
            DrivingLicenceDeliveranceDate = new DateTime(2015, 6, 1)
            // DrivingLicenceNumber missing
        };

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldRejectUnknownCountry()
    {
        await RunAsAgencyAdministratorAsync();

        var command = new CreateClientCommand
        {
            FirstName = "John",
            LastName = "Doe",
            BirthDate = new DateTime(1990, 5, 20),
            BirthCountryId = 999999
        };

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldCreateClient()
    {
        var userId = await RunAsAgencyAdministratorAsync();

        var agencyId = await AddTestAgencyAsync();

        var country = new Country { Name = "Clientland" };
        await AddAsync(country);

        var command = new CreateClientCommand
        {
            FirstName = "John",
            LastName = "Doe",
            BirthDate = new DateTime(1990, 5, 20),
            BirthCountryId = country.Id,
            CIN = "AB123456",
            CINDeliveranceDate = new DateTime(2010, 4, 15),
            CINDeliveranceCountryId = country.Id,
            DrivingLicenceNumber = "12/345678",
            DrivingLicenceDeliveranceDate = new DateTime(2012, 9, 1)
        };

        var clientId = await SendAsync(command);

        var client = await FindAsync<Client>(clientId);

        client.Should().NotBeNull();
        client!.FirstName.Should().Be("John");
        client.LastName.Should().Be("Doe");
        client.BirthDate.Should().Be(new DateTime(1990, 5, 20));
        client.CIN.Should().Be("AB123456");
        client.AgencyId.Should().Be(agencyId);
        client.MarketplaceUserId.Should().BeNull();
        client.CreatedBy.Should().Be(userId);
    }

    /// <summary>
    /// The agent types what is printed on the card; the "valid until" the agency
    /// then gets warned about — and refuses a hire on — is worked out from the
    /// issue date and the agency's default validity.
    /// </summary>
    [Test]
    public async Task ShouldDeriveMissingExpiryDatesFromTheIssueDates()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var issued = new DateTime(2015, 7, 4, 0, 0, 0, DateTimeKind.Utc);
        var onThePassport = new DateTime(2028, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        var clientId = await SendAsync(new CreateClientCommand
        {
            FirstName = "Derived",
            LastName = "Dates",
            BirthDate = new DateTime(1990, 5, 20),
            CIN = "CD654321",
            CINDeliveranceDate = issued,
            PasseportNumber = "P9988776",
            PasseportDeliveranceDate = issued,
            PasseportExpiryDate = onThePassport,
            DrivingLicenceNumber = "12/998877",
            DrivingLicenceDeliveranceDate = issued
        });

        var client = await FindAsync<Client>(clientId);

        // Agency defaults: CIN 10 years, licence 10 years.
        client!.CINExpiryDate.Should().Be(issued.AddYears(10));
        client.DrivingLicenceExpiryDate.Should().Be(issued.AddYears(10));
        // Typed in, so kept as typed rather than derived to 2020.
        client.PasseportExpiryDate.Should().Be(onThePassport);
    }
}
