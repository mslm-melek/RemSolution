using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Renting.Commands.CreateRentingCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Rentings.Commands;

using static Testing;

public class CreateRentingTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 1, 4, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ShouldRequireMinimumFields()
    {
        await RunAsAgencyAdministratorAsync();

        await FluentActions.Invoking(() => SendAsync(new CreateRentingCommand()))
            .Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldCreateRentingWithPriceSnapshot()
    {
        var userId = await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        var carId = await SeedBookableCarAsync(dailyRate: 50m);
        var clientId = await SeedClientAsync();

        var id = await SendAsync(new CreateRentingCommand
        {
            CarId = carId,
            ClientId = clientId,
            StartDate = Start,
            EndDate = End,
            StartMileage = 1000
        });

        var renting = await FindAsync<Renting>(id);

        renting.Should().NotBeNull();
        renting!.AgencyId.Should().Be(agencyId);
        renting.CreatedBy.Should().Be(userId);
        renting.RentingState.Should().Be(RentingState.NotYet);
        // 3 billed days × 50.
        renting.Price!.Amount.Should().Be(150m);
        renting.Price.Currency.Should().Be("TND");
    }

    [Test]
    public async Task ShouldRejectOverlappingRenting()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        var carId = await SeedBookableCarAsync(dailyRate: 50m);
        var clientId = await SeedClientAsync();

        await SendAsync(new CreateRentingCommand
        {
            CarId = carId, ClientId = clientId, StartDate = Start, EndDate = End
        });

        // Overlaps [Start, End).
        await FluentActions.Invoking(() => SendAsync(new CreateRentingCommand
        {
            CarId = carId, ClientId = clientId, StartDate = Start.AddDays(1), EndDate = End.AddDays(1)
        })).Should().ThrowAsync<BookingConflictException>();
    }

    [Test]
    public async Task ShouldAllowBackToBackRentings()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        var carId = await SeedBookableCarAsync(dailyRate: 50m);
        var clientId = await SeedClientAsync();

        await SendAsync(new CreateRentingCommand
        {
            CarId = carId, ClientId = clientId, StartDate = Start, EndDate = End
        });

        // Starts exactly when the first ends — half-open ranges don't overlap.
        var secondId = await SendAsync(new CreateRentingCommand
        {
            CarId = carId, ClientId = clientId, StartDate = End, EndDate = End.AddDays(2)
        });

        (await FindAsync<Renting>(secondId)).Should().NotBeNull();
    }

    [Test]
    public async Task ShouldRejectUnpricedCar()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        var carId = await SeedBookableCarAsync(dailyRate: null);
        var clientId = await SeedClientAsync();

        await FluentActions.Invoking(() => SendAsync(new CreateRentingCommand
        {
            CarId = carId, ClientId = clientId, StartDate = Start, EndDate = End
        })).Should().ThrowAsync<ValidationException>();
    }

    private static async Task<int> SeedBookableCarAsync(decimal? dailyRate)
    {
        var car = new Car
        {
            Matricule = "RENT-1",
            Status = CarStatus.Active,
            DailyRate = dailyRate is decimal r ? Money.Of(r, "TND") : null,
        };
        await AddAsync(car);
        return car.Id;
    }

    private static async Task<int> SeedClientAsync()
    {
        var client = new Client { FirstName = "Test", LastName = "Client" };
        await AddAsync(client);
        return client.Id;
    }
}
