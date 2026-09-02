using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Renting.Commands.CreateRentingCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Rentings.Commands;

using static Testing;

/// <summary>
/// Handing keys to a driver whose licence has lapsed lets the insurer decline
/// the claim, and the liability lands on the agency — so the booking is refused
/// unless the desk says explicitly that it has seen a valid licence.
/// </summary>
public class ExpiredLicenceTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 1, 4, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ShouldRefuseAHireToADriverWithAnExpiredLicence()
    {
        await SetUpAgencyAsync();
        var carId = await SeedCarAsync();
        var clientId = await SeedClientAsync("Lapsed", licenceExpiry: DateTime.UtcNow.Date.AddDays(-1));

        await FluentActions.Invoking(() => SendAsync(new CreateRentingCommand
        {
            CarId = carId, ClientId = clientId, StartDate = Start, EndDate = End
        })).Should().ThrowAsync<ValidationException>();

        (await CountAsync<Renting>()).Should().Be(0);
    }

    /// <summary>
    /// The override exists because the counter sometimes holds a renewal that is
    /// not on file yet — and a rule with no way past it gets worked around by
    /// typing a wrong expiry date instead, which is worse.
    /// </summary>
    [Test]
    public async Task ShouldAllowTheHireWhenTheDeskAcknowledgesIt()
    {
        await SetUpAgencyAsync();
        var carId = await SeedCarAsync();
        var clientId = await SeedClientAsync("Lapsed", licenceExpiry: DateTime.UtcNow.Date.AddDays(-1));

        var id = await SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            ClientId = clientId,
            StartDate = Start,
            EndDate = End,
            AcknowledgeExpiredDocuments = true
        });

        id.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task ShouldAllowAHireOnALicenceExpiringToday()
    {
        // "Valid until today" is good for the whole of today.
        await SetUpAgencyAsync();
        var carId = await SeedCarAsync();
        var clientId = await SeedClientAsync("Today", licenceExpiry: DateTime.UtcNow.Date);

        var id = await SendAsync(new CreateRentingCommand
        {
            CarId = carId, ClientId = clientId, StartDate = Start, EndDate = End
        });

        id.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// No recorded expiry is not the same as valid, but refusing every client
    /// whose paperwork predates the field would take the agency off the road.
    /// </summary>
    [Test]
    public async Task ShouldAllowAHireWhenNoExpiryIsRecorded()
    {
        await SetUpAgencyAsync();
        var carId = await SeedCarAsync();
        var clientId = await SeedClientAsync("Unknown", licenceExpiry: null);

        var id = await SendAsync(new CreateRentingCommand
        {
            CarId = carId, ClientId = clientId, StartDate = Start, EndDate = End
        });

        id.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// The second driver is precisely the gap in an insurance file, and is no
    /// less likely to be the one behind the wheel.
    /// </summary>
    [Test]
    public async Task ShouldRefuseAHireWhoseSecondDriversLicenceHasExpired()
    {
        await SetUpAgencyAsync();
        var carId = await SeedCarAsync();
        var renterId = await SeedClientAsync("Renter", licenceExpiry: DateTime.UtcNow.Date.AddYears(2));
        var secondId = await SeedClientAsync("Second", licenceExpiry: DateTime.UtcNow.Date.AddDays(-30));

        await FluentActions.Invoking(() => SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            ClientId = renterId,
            SecondClientId = secondId,
            StartDate = Start,
            EndDate = End
        })).Should().ThrowAsync<ValidationException>();
    }

    private static async Task SetUpAgencyAsync()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });
    }

    private static async Task<int> SeedCarAsync()
    {
        var car = new Car
        {
            Matricule = "LIC-1", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND")
        };

        await AddAsync(car);
        return car.Id;
    }

    private static async Task<int> SeedClientAsync(string firstName, DateTime? licenceExpiry)
    {
        var client = new Client
        {
            FirstName = firstName,
            LastName = "Driver",
            DrivingLicenceNumber = "12345678",
            DrivingLicenceExpiryDate = licenceExpiry,
        };

        await AddAsync(client);
        return client.Id;
    }
}
