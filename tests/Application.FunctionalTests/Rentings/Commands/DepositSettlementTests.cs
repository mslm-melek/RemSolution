using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Renting.Booking;
using RemSolution.Application.Features.Renting.Commands.ChangeRentingStateCommand;
using RemSolution.Application.Features.Renting.Commands.SettleRentingDepositCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Rentings.Commands;

using static Testing;

/// <summary>
/// What becomes of the deposit at the return. The decision is recorded on the
/// hire and the money moves as a refund on the ledger — the balance is computed
/// from the ledger, so a deposit handed back without one would leave the client
/// looking as though they had paid money they no longer have.
/// </summary>
public class DepositSettlementTests : BaseTestFixture
{
    [Test]
    public async Task ReturningWithoutSettling_ShouldLeaveTheDepositOpen()
    {
        var rentingId = await ARunningHireWithDepositAsync();

        await SendAsync(new ChangeRentingStateCommand
        {
            Id = rentingId, NewState = RentingState.Done, Mileage = 1_200
        });

        var renting = await FindAsync<Renting>(rentingId);

        renting!.RentingState.Should().Be(RentingState.Done);
        // Not assumed refunded: it stays on the desk's list.
        renting.DepositSettledAt.Should().BeNull();
        renting.HasUnsettledDeposit.Should().BeTrue();
        (await RefundCountAsync(rentingId)).Should().Be(0);
    }

    [Test]
    public async Task ReturningWithAFullRefund_ShouldRecordItAndPayItBack()
    {
        var rentingId = await ARunningHireWithDepositAsync();

        await SendAsync(new ChangeRentingStateCommand
        {
            Id = rentingId,
            NewState = RentingState.Done,
            Mileage = 1_200,
            DepositSettlement = new DepositSettlementPayload { RetainedAmount = 0m }
        });

        var renting = await FindAsync<Renting>(rentingId);

        renting!.HasUnsettledDeposit.Should().BeFalse();
        renting.DepositRetainedAmount.Should().BeNull();

        var refund = await SingleRefundAsync(rentingId);
        refund.PayementAmount!.Amount.Should().Be(-300m);
        refund.PayementAmount.Currency.Should().Be("TND");
        refund.IsRefund.Should().BeTrue();
        refund.ClientId.Should().Be(renting.ClientId);
    }

    [Test]
    public async Task ReturningWithAPartialRetention_ShouldRefundOnlyTheRest()
    {
        var rentingId = await ARunningHireWithDepositAsync();

        await SendAsync(new ChangeRentingStateCommand
        {
            Id = rentingId,
            NewState = RentingState.Done,
            Mileage = 1_200,
            DepositSettlement = new DepositSettlementPayload
            {
                RetainedAmount = 120m,
                RefundMethod = PaymentMethod.Transfer,
                Note = "Rear bumper"
            }
        });

        var renting = await FindAsync<Renting>(rentingId);
        renting!.DepositRetainedAmount!.Amount.Should().Be(120m);

        var refund = await SingleRefundAsync(rentingId);
        refund.PayementAmount!.Amount.Should().Be(-180m);
        refund.Method.Should().Be(PaymentMethod.Transfer);
        refund.Notes.Should().Be("Rear bumper");
    }

    [Test]
    public async Task KeepingTheWholeDeposit_ShouldRecordNoRefund()
    {
        var rentingId = await ARunningHireWithDepositAsync();

        await SendAsync(new ChangeRentingStateCommand
        {
            Id = rentingId,
            NewState = RentingState.Done,
            Mileage = 1_200,
            DepositSettlement = new DepositSettlementPayload { RetainedAmount = 300m }
        });

        (await FindAsync<Renting>(rentingId))!.DepositRetainedAmount!.Amount.Should().Be(300m);
        // Nothing went back, so there is no ledger entry to write.
        (await RefundCountAsync(rentingId)).Should().Be(0);
    }

    [Test]
    public async Task KeepingMoreThanIsHeld_ShouldBeRefused()
    {
        var rentingId = await ARunningHireWithDepositAsync();

        await FluentActions.Invoking(() => SendAsync(new ChangeRentingStateCommand
        {
            Id = rentingId,
            NewState = RentingState.Done,
            Mileage = 1_200,
            DepositSettlement = new DepositSettlementPayload { RetainedAmount = 301m }
        })).Should().ThrowAsync<Domain.Exceptions.DomainRuleException>();
    }

    [Test]
    public async Task SettlingOnAPickup_ShouldBeRefused()
    {
        var rentingId = await AnUpcomingHireWithDepositAsync();

        await FluentActions.Invoking(() => SendAsync(new ChangeRentingStateCommand
        {
            Id = rentingId,
            NewState = RentingState.InProgress,
            DepositSettlement = new DepositSettlementPayload { RetainedAmount = 0m }
        })).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task SettlingAfterTheFact_ShouldWorkThroughItsOwnCommand()
    {
        var rentingId = await ARunningHireWithDepositAsync();

        await SendAsync(new ChangeRentingStateCommand
        {
            Id = rentingId, NewState = RentingState.Done, Mileage = 1_200
        });

        await SendAsync(new SettleRentingDepositCommand
        {
            RentingId = rentingId,
            Settlement = new DepositSettlementPayload { RetainedAmount = 50m }
        });

        (await FindAsync<Renting>(rentingId))!.HasUnsettledDeposit.Should().BeFalse();
        (await SingleRefundAsync(rentingId)).PayementAmount!.Amount.Should().Be(-250m);
    }

    /// <summary>
    /// Paying a deposit back twice is the mistake this whole pair of paths is
    /// meant to prevent, so a second settlement is refused rather than ignored.
    /// </summary>
    [Test]
    public async Task SettlingTwice_ShouldBeRefused()
    {
        var rentingId = await ARunningHireWithDepositAsync();

        await SendAsync(new ChangeRentingStateCommand
        {
            Id = rentingId,
            NewState = RentingState.Done,
            Mileage = 1_200,
            DepositSettlement = new DepositSettlementPayload { RetainedAmount = 0m }
        });

        await FluentActions.Invoking(() => SendAsync(new SettleRentingDepositCommand
        {
            RentingId = rentingId,
            Settlement = new DepositSettlementPayload { RetainedAmount = 0m }
        })).Should().ThrowAsync<ValidationException>();

        (await RefundCountAsync(rentingId)).Should().Be(1);
    }

    [Test]
    public async Task SettlingAHireWithNoDeposit_ShouldBeRefused()
    {
        await SetUpAgencyAsync();
        var carId = await SeedCarAsync();
        var clientId = await SeedClientAsync();

        var renting = RentingFixture.Hire(carId, clientId, price: Money.Of(400m, "TND"));
        await AddAsync(renting);

        await FluentActions.Invoking(() => SendAsync(new SettleRentingDepositCommand
        {
            RentingId = renting.Id,
            Settlement = new DepositSettlementPayload { RetainedAmount = 0m }
        })).Should().ThrowAsync<ValidationException>();
    }

    private static async Task<int> ARunningHireWithDepositAsync()
    {
        await SetUpAgencyAsync();
        var carId = await SeedCarAsync();
        var clientId = await SeedClientAsync();

        var renting = RentingFixture.Hire(
            carId, clientId, state: RentingState.InProgress,
            price: Money.Of(400m, "TND"), startMileage: 1_000,
            depositAmount: Money.Of(300m, "TND"));

        await AddAsync(renting);
        return renting.Id;
    }

    private static async Task<int> AnUpcomingHireWithDepositAsync()
    {
        await SetUpAgencyAsync();
        var carId = await SeedCarAsync();
        var clientId = await SeedClientAsync();

        var renting = RentingFixture.Hire(
            carId, clientId, price: Money.Of(400m, "TND"), depositAmount: Money.Of(300m, "TND"));

        await AddAsync(renting);
        return renting.Id;
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
            Matricule = "DEP-1", Status = CarStatus.Active, DailyRate = Money.Of(100m, "TND")
        };

        await AddAsync(car);
        return car.Id;
    }

    private static async Task<int> SeedClientAsync()
    {
        var client = new Client { FirstName = "Deposit", LastName = "Client" };
        await AddAsync(client);
        return client.Id;
    }

    private static Task<int> RefundCountAsync(int rentingId) =>
        CountAsync<Payment>(p => p.RentingId == rentingId && p.IsRefund);

    private static async Task<Payment> SingleRefundAsync(int rentingId)
    {
        var refunds = (await AllAsync<Payment>())
            .Where(p => p.RentingId == rentingId && p.IsRefund)
            .ToList();

        refunds.Should().HaveCount(1);
        return refunds[0];
    }
}
