using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Renting.Commands.ChangeRentingStateCommand;
using RemSolution.Application.Features.Renting.Commands.CreateRentingCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Rentings.Commands;

using static Testing;

public class ChangeRentingStateTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 3, 5, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ShouldRunFullLifecycleAndWriteHistory()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });
        var rentingId = await SeedRentingAsync();

        await SendAsync(new ChangeRentingStateCommand
        {
            Id = rentingId, NewState = RentingState.InProgress, Mileage = 1000
        });

        (await FindAsync<Renting>(rentingId))!.RentingState.Should().Be(RentingState.InProgress);

        await SendAsync(new ChangeRentingStateCommand
        {
            Id = rentingId, NewState = RentingState.Done, Mileage = 1500
        });

        var renting = await FindAsync<Renting>(rentingId);
        renting!.RentingState.Should().Be(RentingState.Done);
        renting.EndMileage.Should().Be(1500);

        // Completion snapshots a history row.
        (await CountAsync<RentingHistory>(h => h.RentingId == rentingId)).Should().Be(1);
    }

    // The car's own odometer is what the next booking offers as its pickup
    // reading, so the readings taken at pickup and return have to reach it.
    [Test]
    public async Task ShouldMoveTheCarsOdometerWithThePickupAndTheReturn()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        var car = new Car
        {
            Matricule = "ODO-1", Status = CarStatus.Active,
            DailyRate = Money.Of(40m, "TND"), Mileage = 20_000
        };
        await AddAsync(car);
        var client = new Client { FirstName = "Odo", LastName = "Meter" };
        await AddAsync(client);

        var rentingId = await SendAsync(new CreateRentingCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End
        });

        // The dashboard read a little more than the record did.
        await SendAsync(new ChangeRentingStateCommand
        {
            Id = rentingId, NewState = RentingState.InProgress, Mileage = 20_150
        });

        (await FindAsync<Car>(car.Id))!.Mileage.Should().Be(20_150);

        await SendAsync(new ChangeRentingStateCommand
        {
            Id = rentingId, NewState = RentingState.Done, Mileage = 20_900
        });

        (await FindAsync<Car>(car.Id))!.Mileage.Should().Be(20_900);
    }

    // A booking taken with a reading at the counter is a reading off the car.
    [Test]
    public async Task ShouldMoveTheCarsOdometerWhenTheBookingCarriesAPickupReading()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        var car = new Car
        {
            Matricule = "ODO-2", Status = CarStatus.Active,
            DailyRate = Money.Of(40m, "TND"), Mileage = 9_000
        };
        await AddAsync(car);
        var client = new Client { FirstName = "Odo", LastName = "Booking" };
        await AddAsync(client);

        await SendAsync(new CreateRentingCommand
        {
            CarId = car.Id, ClientId = client.Id,
            StartDate = Start, EndDate = End, StartMileage = 9_400
        });

        (await FindAsync<Car>(car.Id))!.Mileage.Should().Be(9_400);
    }

    [Test]
    public async Task ShouldRejectCompletingAnUpcomingRenting()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });
        var rentingId = await SeedRentingAsync();

        // NotYet → Done is not a valid forward transition.
        await FluentActions.Invoking(() => SendAsync(new ChangeRentingStateCommand
        {
            Id = rentingId, NewState = RentingState.Done
        })).Should().ThrowAsync<ValidationException>();
    }

    private static async Task<int> SeedRentingAsync()
    {
        var car = new Car { Matricule = "STATE-1", Status = CarStatus.Active, DailyRate = Money.Of(40m, "TND") };
        await AddAsync(car);
        var client = new Client { FirstName = "Test", LastName = "Client" };
        await AddAsync(client);

        return await SendAsync(new CreateRentingCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End
        });
    }
}
