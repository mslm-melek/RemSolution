using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Credit.Queries.GetClientCreditsQuery;
using RemSolution.Application.Features.Payment.Commands.CreatePaymentCommand;
using RemSolution.Application.Features.Payment.Queries.GetClientBalanceQuery;
using RemSolution.Application.Features.Renting.Commands.CancelRentingCommand;
using RemSolution.Application.Features.Renting.Commands.CreateRentingCommand;
using RemSolution.Application.Features.Renting.Queries.GetRentingByIdQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Rentings.Commands;

using static Testing;

/// <summary>
/// Cancelling is a money decision as much as a state change: what the client is
/// left owing, and what happens to whatever they already paid. These tests pin
/// both, and pin that every screen showing the client's debt agrees about it (see
/// ClientCreditRows).
/// </summary>
public class CancelRentingTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 6, 6, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ShouldWriteOffTheWholePriceWhenCancelledForFree()
    {
        await RunAsAgencyAdministratorAsync();
        await SetUpAsync();
        var (rentingId, clientId) = await SeedRentingAsync("FREE-1");

        // 5 days at 100 = 500 charged before anything happens.
        (await ChargedAsync(clientId)).Should().Be(500m);

        await SendAsync(new CancelRentingCommand { Id = rentingId });

        var renting = await FindAsync<Renting>(rentingId);
        renting!.RentingState.Should().Be(RentingState.Cancelled);
        renting.CancellationFee.Should().BeNull();

        // Nothing is owed on a hire that was called off for free — and the row's
        // own outstanding figure says so too, even though it was never paid.
        (await ChargedAsync(clientId)).Should().Be(0m);
        (await SendAsync(new GetRentingByIdQuery(rentingId)))!
            .Outstanding!.Amount.Should().Be(0m);
    }

    [Test]
    public async Task ShouldKeepTheFeeOwedWhenOneIsCharged()
    {
        await RunAsAgencyAdministratorAsync();
        await SetUpAsync();
        var (rentingId, clientId) = await SeedRentingAsync("FEE-1");

        await SendAsync(new CancelRentingCommand { Id = rentingId, CancellationFee = 150m });

        (await FindAsync<Renting>(rentingId))!.CancellationFee!.Amount.Should().Be(150m);

        // The fee replaces the price as what the hire charges: the client owes the
        // 150 and is let off the other 350.
        (await ChargedAsync(clientId)).Should().Be(150m);
        (await SendAsync(new GetRentingByIdQuery(rentingId)))!
            .Outstanding!.Amount.Should().Be(150m);
    }

    [Test]
    public async Task ShouldRefundWhatWasPaidBeyondTheFee()
    {
        await RunAsAgencyAdministratorAsync();
        await SetUpAsync();
        var (rentingId, clientId) = await SeedRentingAsync("REFUND-1");

        await SendAsync(new CreatePaymentCommand { RentingId = rentingId, Amount = 200m });

        await SendAsync(new CancelRentingCommand
        {
            Id = rentingId, CancellationFee = 150m, RefundExcess = true
        });

        // 200 collected, 150 kept: exactly 50 goes back, as a negative entry.
        var refunds = await AllAsync<Payment>();
        var refund = refunds.Single(p => p.RentingId == rentingId && p.IsRefund);
        refund.PayementAmount!.Amount.Should().Be(-50m);

        // Which leaves the hire settled: 150 charged, 150 net collected.
        (await ChargedAsync(clientId)).Should().Be(150m);
        (await PaidAsync(clientId)).Should().Be(150m);
        (await SendAsync(new GetRentingByIdQuery(rentingId)))!
            .Outstanding!.Amount.Should().Be(0m);
    }

    [Test]
    public async Task ShouldLeaveWhatWasPaidAsACreditWhenNotRefunding()
    {
        await RunAsAgencyAdministratorAsync();
        await SetUpAsync();
        var (rentingId, clientId) = await SeedRentingAsync("CREDIT-1");

        await SendAsync(new CreatePaymentCommand { RentingId = rentingId, Amount = 200m });

        // Cancelled for free and nothing handed back: the money stays on the
        // ledger, so the agency now owes the client 200.
        await SendAsync(new CancelRentingCommand { Id = rentingId });

        (await AllAsync<Payment>()).Where(p => p.IsRefund).Should().BeEmpty();
        (await ChargedAsync(clientId)).Should().Be(0m);
        (await PaidAsync(clientId)).Should().Be(200m);

        var balance = await SendAsync(new GetClientBalanceQuery(clientId));
        balance!.Balance!.Amount.Should().Be(-200m);
    }

    [Test]
    public async Task ShouldRefundNothingWhenTheFeeCoversWhatWasPaid()
    {
        await RunAsAgencyAdministratorAsync();
        await SetUpAsync();
        var (rentingId, _) = await SeedRentingAsync("NOEXCESS-1");

        await SendAsync(new CreatePaymentCommand { RentingId = rentingId, Amount = 100m });

        // Asking to refund when the fee swallows the whole payment writes nothing:
        // there is no excess to hand back.
        await SendAsync(new CancelRentingCommand
        {
            Id = rentingId, CancellationFee = 250m, RefundExcess = true
        });

        (await AllAsync<Payment>()).Where(p => p.IsRefund).Should().BeEmpty();
    }

    [Test]
    public async Task ShouldRejectAFeeOnAPricelessRenting()
    {
        await RunAsAgencyAdministratorAsync();
        await SetUpAsync();

        // A courtesy car booked at no charge: there is no price for a fee to be a
        // part of, and no balance on the row to show one (see RentingDto).
        var car = new Car { Matricule = "NOPRICE-1", Status = CarStatus.Active };
        await AddAsync(car);
        var client = new Client { FirstName = "Free", LastName = "Rider" };
        await AddAsync(client);
        var renting = new Renting
        {
            CarId = car.Id, ClientId = client.Id,
            StartDate = Start, EndDate = End, RentingState = RentingState.NotYet
        };
        await AddAsync(renting);

        await FluentActions.Invoking(() => SendAsync(new CancelRentingCommand
        {
            Id = renting.Id, CancellationFee = 50m
        })).Should().ThrowAsync<ValidationException>();

        // Cancelling it for free is still fine.
        await SendAsync(new CancelRentingCommand { Id = renting.Id });
        (await FindAsync<Renting>(renting.Id))!.RentingState.Should().Be(RentingState.Cancelled);
    }

    [Test]
    public async Task ShouldRejectAFeeAboveThePrice()
    {
        await RunAsAgencyAdministratorAsync();
        await SetUpAsync();
        var (rentingId, _) = await SeedRentingAsync("TOOBIG-1");

        // A cancelled hire cannot cost more than the hire itself.
        await FluentActions.Invoking(() => SendAsync(new CancelRentingCommand
        {
            Id = rentingId, CancellationFee = 501m
        })).Should().ThrowAsync<ValidationException>();

        (await FindAsync<Renting>(rentingId))!.RentingState.Should().Be(RentingState.NotYet);
    }

    // The credits list, the client balance and the row itself are three readings of
    // the same arithmetic (see ClientCreditRows); a cancelled hire with a fee is
    // exactly the case where copies of it would have drifted apart.
    [Test]
    public async Task EveryDebtScreenShouldAgreeOnACancelledHireWithAFee()
    {
        await RunAsAgencyAdministratorAsync();
        await SetUpAsync();
        var (rentingId, clientId) = await SeedRentingAsync("AGREE-1");

        await SendAsync(new CreatePaymentCommand { RentingId = rentingId, Amount = 40m });
        await SendAsync(new CancelRentingCommand { Id = rentingId, CancellationFee = 150m });

        var credits = await SendAsync(new GetClientCreditsQuery(OnlyOutstanding: false, PageSize: 50));
        var row = credits.Items.Single(r => r.ClientId == clientId);

        var balance = await SendAsync(new GetClientBalanceQuery(clientId));

        row.Charged!.Amount.Should().Be(150m);
        row.Outstanding!.Amount.Should().Be(110m);
        balance!.TotalCharged!.Amount.Should().Be(150m);
        balance.Balance!.Amount.Should().Be(110m);
    }

    private static async Task SetUpAsync()
    {
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });
    }

    private static async Task<(int RentingId, int ClientId)> SeedRentingAsync(string matricule)
    {
        var car = new Car
        {
            Matricule = matricule, Status = CarStatus.Active, DailyRate = Money.Of(100m, "TND")
        };
        await AddAsync(car);

        var client = new Client { FirstName = "Cancel", LastName = "Test" };
        await AddAsync(client);

        var rentingId = await SendAsync(new CreateRentingCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End
        });

        return (rentingId, client.Id);
    }

    // Read through the shared projection's own consumer, so these assertions break
    // if the charge rule moves.
    private static async Task<decimal> ChargedAsync(int clientId)
    {
        var credits = await SendAsync(new GetClientCreditsQuery(OnlyOutstanding: false, PageSize: 50));
        return credits.Items.Single(r => r.ClientId == clientId).Charged!.Amount;
    }

    private static async Task<decimal> PaidAsync(int clientId)
    {
        var credits = await SendAsync(new GetClientCreditsQuery(OnlyOutstanding: false, PageSize: 50));
        return credits.Items.Single(r => r.ClientId == clientId).Paid!.Amount;
    }
}
