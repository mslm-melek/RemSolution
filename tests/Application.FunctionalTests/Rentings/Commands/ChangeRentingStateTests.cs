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
