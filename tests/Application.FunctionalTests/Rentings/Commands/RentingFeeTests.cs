using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Credit.Queries.GetClientCreditsByIdsQuery;
using RemSolution.Application.Features.Payment.Commands.CreatePaymentCommand;
using RemSolution.Application.Features.Renting.Booking;
using RemSolution.Application.Features.Renting.Commands.AddRentingFeeCommand;
using RemSolution.Application.Features.Renting.Commands.CancelRentingCommand;
using RemSolution.Application.Features.Renting.Commands.ChangeRentingStateCommand;
using RemSolution.Application.Features.Renting.Commands.CreateRentingCommand;
using RemSolution.Application.Features.Renting.Commands.DeleteRentingFeeCommand;
using RemSolution.Application.Features.Renting.Queries.GetRentingByIdQuery;
using RemSolution.Application.Features.Renting.Queries.GetRentingFeesQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.Exceptions;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Rentings.Commands;

using static Testing;

// The charges established when the car comes back (§3.2): entered in the return
// itself, added to what the hire charges, and correctable afterwards.
public class RentingFeeTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 7, 4, 0, 0, 0, DateTimeKind.Utc);

    // 3 days × 40 = 120 TND, out on the road and waiting to be brought back.
    private async Task<(int RentingId, int ClientId)> HireInProgressAsync(string matricule)
    {
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Payments, Enabled = true });
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Credits, Enabled = true });

        var car = new Car { Matricule = matricule, Status = CarStatus.Active, DailyRate = Money.Of(40m, "TND") };
        await AddAsync(car);
        var client = new Client { FirstName = "Fee", LastName = "Client" };
        await AddAsync(client);

        var id = await SendAsync(new CreateRentingCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End, StartMileage = 500
        });

        await SendAsync(new ChangeRentingStateCommand
        {
            Id = id, NewState = RentingState.InProgress, Mileage = 500
        });

        return (id, client.Id);
    }

    [Test]
    public async Task TheReturnBooksItsChargesInTheSameWriteThatClosesTheHire()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (id, _) = await HireInProgressAsync("FEE-1");

        await SendAsync(new ChangeRentingStateCommand
        {
            Id = id,
            NewState = RentingState.Done,
            Mileage = 900,
            Fees = new List<RentingFeePayload>
            {
                new() { Kind = RentingFeeKind.Late, Amount = 30m, Note = "Two hours late" },
                new() { Kind = RentingFeeKind.Fuel, Amount = 45m },
            }
        });

        var renting = await FindAsync<Renting>(id);
        renting!.RentingState.Should().Be(RentingState.Done);

        var fees = await SendAsync(new GetRentingFeesQuery(id));
        fees.Should().HaveCount(2);
        fees[0].Kind.Should().Be(RentingFeeKind.Late);
        fees[0].Note.Should().Be("Two hours late");
        // The hire's currency, never the caller's — nothing sent one.
        fees[0].Amount!.Currency.Should().Be("TND");
    }

    // The price is what was agreed for the rental; the charges ride beside it.
    [Test]
    public async Task ChargesAddToWhatTheHireOwesWithoutMovingItsPrice()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (id, clientId) = await HireInProgressAsync("FEE-2");

        await SendAsync(new ChangeRentingStateCommand
        {
            Id = id,
            NewState = RentingState.Done,
            Mileage = 900,
            Fees = new List<RentingFeePayload> { new() { Kind = RentingFeeKind.Damage, Amount = 80m } }
        });

        var dto = await SendAsync(new GetRentingByIdQuery(id));
        dto!.Price!.Amount.Should().Be(120m);
        dto.Fees!.Amount.Should().Be(80m);
        dto.Outstanding!.Amount.Should().Be(200m);

        // And the client's balance says the same thing (see ClientCreditRows).
        var credit = (await SendAsync(new GetClientCreditsByIdsQuery(new[] { clientId }))).Single();
        credit.Charged!.Amount.Should().Be(200m);
        credit.Outstanding!.Amount.Should().Be(200m);
    }

    // The ceiling CreatePaymentCommand enforces has to move with the charges, or
    // the counter cannot collect the money it just billed.
    [Test]
    public async Task APaymentMayCoverThePriceAndTheChargesButNoMore()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (id, _) = await HireInProgressAsync("FEE-3");

        await SendAsync(new ChangeRentingStateCommand
        {
            Id = id,
            NewState = RentingState.Done,
            Mileage = 900,
            Fees = new List<RentingFeePayload> { new() { Kind = RentingFeeKind.Cleaning, Amount = 20m } }
        });

        await SendAsync(new CreatePaymentCommand { RentingId = id, Amount = 140m });

        (await SendAsync(new GetRentingByIdQuery(id)))!.Outstanding!.Amount.Should().Be(0m);

        await FluentActions.Invoking(() => SendAsync(new CreatePaymentCommand { RentingId = id, Amount = 1m }))
            .Should().ThrowAsync<ValidationException>();
    }

    // Charges are things found on a returning car; a pickup carrying them is a
    // caller's mistake, not something to swallow.
    [Test]
    public async Task ChargesAreRefusedOnAnythingButTheReturn()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        var car = new Car { Matricule = "FEE-4", Status = CarStatus.Active, DailyRate = Money.Of(40m, "TND") };
        await AddAsync(car);
        var client = new Client { FirstName = "Fee", LastName = "Pickup" };
        await AddAsync(client);

        var id = await SendAsync(new CreateRentingCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End, StartMileage = 500
        });

        await FluentActions.Invoking(() => SendAsync(new ChangeRentingStateCommand
        {
            Id = id,
            NewState = RentingState.InProgress,
            Fees = new List<RentingFeePayload> { new() { Kind = RentingFeeKind.Late, Amount = 10m } }
        })).Should().ThrowAsync<ValidationException>();

        (await CountAsync<RentingFee>()).Should().Be(0);
    }

    // The dent the workshop finds the next morning, and the one it turns out was
    // already on file.
    [Test]
    public async Task AChargeCanBeAddedAndTakenBackOffAfterTheHireIsClosed()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (id, _) = await HireInProgressAsync("FEE-5");

        await SendAsync(new ChangeRentingStateCommand { Id = id, NewState = RentingState.Done, Mileage = 900 });

        var feeId = await SendAsync(new AddRentingFeeCommand
        {
            RentingId = id,
            Fee = new RentingFeePayload
            {
                Kind = RentingFeeKind.Damage, Amount = 250m, Note = "Rear bumper"
            }
        });

        (await SendAsync(new GetRentingByIdQuery(id)))!.Outstanding!.Amount.Should().Be(370m);

        await SendAsync(new DeleteRentingFeeCommand(id, feeId));

        (await CountAsync<RentingFee>(f => f.RentingId == id)).Should().Be(0);
        (await SendAsync(new GetRentingByIdQuery(id)))!.Outstanding!.Amount.Should().Be(120m);
    }

    // A charge moves what a client owes and a deletion destroys the row that said
    // so, which makes the audit's Before the only surviving record of it. The
    // audited entity therefore has to be the CHARGE: naming the hire would match
    // nothing in the change tracker (the Renting row is untouched by both) and
    // write no row at all.
    [Test]
    public async Task AddingAndRemovingAChargeBothLeaveAnAuditTrail()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (id, _) = await HireInProgressAsync("FEE-7");

        var feeId = await SendAsync(new AddRentingFeeCommand
        {
            RentingId = id,
            Fee = new RentingFeePayload { Kind = RentingFeeKind.Damage, Amount = 250m }
        });

        (await CountAsync<AuditLog>(l => l.Action == "AddRentingFee")).Should().Be(1);

        await SendAsync(new DeleteRentingFeeCommand(id, feeId));

        var erased = (await AllAsync<AuditLog>()).Single(l => l.Action == "DeleteRentingFee");
        erased.Entity.Should().Be(nameof(RentingFee));
        // What the client was charged is still readable once the row is gone.
        erased.Before.Should().Contain("250");
        erased.After.Should().BeNull();
    }

    // A charge booked while the car was out survives the hire being cancelled —
    // the charge rule still bills it — so it has to stay removable, or it is
    // stranded on the client's balance for good.
    [Test]
    public async Task AChargeStaysRemovableAfterTheHireIsCancelled()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (id, clientId) = await HireInProgressAsync("FEE-8");

        var feeId = await SendAsync(new AddRentingFeeCommand
        {
            RentingId = id,
            Fee = new RentingFeePayload { Kind = RentingFeeKind.Damage, Amount = 250m }
        });

        await SendAsync(new CancelRentingCommand { Id = id });

        // Cancelled for free, so the price is off the balance but the charge is not.
        var owed = (await SendAsync(new GetClientCreditsByIdsQuery(new[] { clientId }))).Single();
        owed.Charged!.Amount.Should().Be(250m);

        await SendAsync(new DeleteRentingFeeCommand(id, feeId));

        var settled = (await SendAsync(new GetClientCreditsByIdsQuery(new[] { clientId }))).Single();
        settled.Charged!.Amount.Should().Be(0m);
    }

    [Test]
    public async Task AChargeIsRefusedOnAHireWhoseCarNeverWentOut()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        var car = new Car { Matricule = "FEE-6", Status = CarStatus.Active, DailyRate = Money.Of(40m, "TND") };
        await AddAsync(car);
        var client = new Client { FirstName = "Fee", LastName = "Upcoming" };
        await AddAsync(client);

        var id = await SendAsync(new CreateRentingCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End
        });

        await FluentActions.Invoking(() => SendAsync(new AddRentingFeeCommand
        {
            RentingId = id,
            Fee = new RentingFeePayload { Kind = RentingFeeKind.Other, Amount = 10m }
        })).Should().ThrowAsync<InvalidRentingTransitionException>();
    }
}
