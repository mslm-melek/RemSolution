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
        var command = new CreateClientCommand(); // empty

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldRequireBirthDate()
    {
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
        var userId = await RunAsDefaultUserAsync();

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
}
