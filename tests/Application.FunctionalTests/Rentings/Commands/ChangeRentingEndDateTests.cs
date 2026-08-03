using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Renting.Commands.ChangeRentingEndDateCommand;
using RemSolution.Application.Features.Renting.Commands.ChangeRentingStateCommand;
using RemSolution.Application.Features.Renting.Commands.CreateRentingCommand;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using ContractEntity = RemSolution.Domain.Entities.Contract;
using RentingEntity = RemSolution.Domain.Entities.Renting;

namespace RemSolution.Application.FunctionalTests.Rentings.Commands;

using static Testing;

/// <summary>
/// Extending a live renting, or taking it back early. The rule that matters here
/// is the one UpdateRentingCommand deliberately does NOT follow: the days the
/// client already agreed to keep their agreed price, and only the difference is
/// quoted at the car's current rate.
/// </summary>
public class ChangeRentingEndDateTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 6, 6, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ExtendingChargesTheAddedDaysAndKeepsTheAgreedPart()
    {
        var (id, car) = await BookedRentingAsync("EXT-1");
        // 5 days × 100 = 500 agreed.

        // The rate goes up after the booking; the agreed days must not follow it.
        car.DailyRate = Money.Of(120m, "TND");
        await UpdateAsync(car);

        await SendAsync(new ChangeRentingEndDateCommand { Id = id, EndDate = End.AddDays(3) });

        var renting = await FindAsync<RentingEntity>(id);
        renting!.EndDate.Should().Be(End.AddDays(3));
        // 500 kept + 3 × 120 = 860, not 8 × 120.
        renting.Price!.Amount.Should().Be(860m);
    }

    [Test]
    public async Task AnEarlyReturnCreditsTheDaysGivenBack()
    {
        var (id, car) = await BookedRentingAsync("EXT-2");

        // A rate rise must not inflate the credit either.
        car.DailyRate = Money.Of(400m, "TND");
        await UpdateAsync(car);

        await SendAsync(new ChangeRentingEndDateCommand { Id = id, EndDate = End.AddDays(-2) });

        var renting = await FindAsync<RentingEntity>(id);
        renting!.EndDate.Should().Be(End.AddDays(-2));
        // 3 of the 5 agreed days: 500 × 3/5.
        renting.Price!.Amount.Should().Be(300m);
    }

    [Test]
    public async Task AnAgreedTotalReplacesTheCalculatedDifference()
    {
        var (id, _) = await BookedRentingAsync("EXT-6");
        // 5 days × 100 = 500 agreed; three more days would come to 800.

        await SendAsync(new ChangeRentingEndDateCommand
        {
            Id = id,
            EndDate = End.AddDays(3),
            // The extension was thrown in at a flat 600.
            PriceOverride = 600m
        });

        var renting = await FindAsync<RentingEntity>(id);
        renting!.EndDate.Should().Be(End.AddDays(3));
        renting.Price!.Amount.Should().Be(600m);
    }

    [Test]
    public async Task ExtendingIntoAnotherBookingIsRefused()
    {
        var (id, car) = await BookedRentingAsync("EXT-3");

        var otherClient = new Client { FirstName = "Second", LastName = "Client" };
        await AddAsync(otherClient);

        // The same car is already taken from the 8th.
        await SendAsync(new CreateRentingCommand
        {
            CarId = car.Id, ClientId = otherClient.Id,
            StartDate = End.AddDays(2), EndDate = End.AddDays(5)
        });

        var act = async () => await SendAsync(new ChangeRentingEndDateCommand
        {
            Id = id, EndDate = End.AddDays(4)
        });

        await act.Should().ThrowAsync<BookingConflictException>();

        // Refused means unchanged, not partially applied.
        var renting = await FindAsync<RentingEntity>(id);
        renting!.EndDate.Should().Be(End);
        renting.Price!.Amount.Should().Be(500m);
    }

    [Test]
    public async Task ExtendingAnInProgressRentingIsAllowed()
    {
        var (id, _) = await BookedRentingAsync("EXT-4");

        await SendAsync(new ChangeRentingStateCommand { Id = id, NewState = RentingState.InProgress });

        await SendAsync(new ChangeRentingEndDateCommand { Id = id, EndDate = End.AddDays(1) });

        var renting = await FindAsync<RentingEntity>(id);
        renting!.EndDate.Should().Be(End.AddDays(1));
        renting.Price!.Amount.Should().Be(600m);
    }

    [Test]
    public async Task ACompletedRentingCanNoLongerBeChanged()
    {
        var (id, _) = await BookedRentingAsync("EXT-5");

        await SendAsync(new ChangeRentingStateCommand { Id = id, NewState = RentingState.InProgress });
        await SendAsync(new ChangeRentingStateCommand { Id = id, NewState = RentingState.Done });

        var act = async () => await SendAsync(new ChangeRentingEndDateCommand
        {
            Id = id, EndDate = End.AddDays(2)
        });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task AnEndDateBeforeTheStartIsRefused()
    {
        var (id, _) = await BookedRentingAsync("EXT-6");

        var act = async () => await SendAsync(new ChangeRentingEndDateCommand
        {
            Id = id, EndDate = Start.AddDays(-1)
        });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ReissuingIssuesANewContractAndKeepsTheOldOne()
    {
        var (id, _) = await BookedRentingAsync("EXT-7", withContract: true);

        var before = await CountAsync<ContractEntity>(c => c.RentingId == id);
        before.Should().Be(1);

        await SendAsync(new ChangeRentingEndDateCommand
        {
            Id = id, EndDate = End.AddDays(2), RegenerateContract = true
        });

        // Append-only: the copy the client already signed stays retrievable.
        var after = await AllAsync<ContractEntity>();
        after.Where(c => c.RentingId == id).Should().HaveCount(2);
        after.Select(c => c.SequenceNumber).Should().OnlyHaveUniqueItems();
    }

    [Test]
    public async Task NotReissuingLeavesThePaperworkAlone()
    {
        var (id, _) = await BookedRentingAsync("EXT-8", withContract: true);

        await SendAsync(new ChangeRentingEndDateCommand
        {
            Id = id, EndDate = End.AddDays(2), RegenerateContract = false
        });

        var contracts = await CountAsync<ContractEntity>(c => c.RentingId == id);
        contracts.Should().Be(1);

        // The renting itself still moved.
        var renting = await FindAsync<RentingEntity>(id);
        renting!.EndDate.Should().Be(End.AddDays(2));
    }

    [Test]
    public async Task ChangingTheEndDateWritesNoRentingHistory()
    {
        var (id, _) = await BookedRentingAsync("EXT-9");

        await SendAsync(new ChangeRentingEndDateCommand { Id = id, EndDate = End.AddDays(1) });

        // History records the snapshot of a FINISHED period, not an amendment to a
        // live one (see RentingCompletedEvent).
        var history = await CountAsync<RentingHistory>();
        history.Should().Be(0);
    }

    // A 5-day renting at 100/day (500 agreed), optionally with its contract.
    private static async Task<(int Id, Car Car)> BookedRentingAsync(
        string matricule, bool withContract = false)
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = new Car { Matricule = matricule, Status = CarStatus.Active, DailyRate = Money.Of(100m, "TND") };
        await AddAsync(car);

        var client = new Client { FirstName = "Extend", LastName = "Client" };
        await AddAsync(client);

        var id = await SendAsync(new CreateRentingCommand
        {
            CarId = car.Id,
            ClientId = client.Id,
            StartDate = Start,
            EndDate = End,
            GenerateContract = withContract
        });

        return (id, car);
    }
}
