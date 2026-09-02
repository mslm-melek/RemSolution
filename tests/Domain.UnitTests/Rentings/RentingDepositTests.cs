using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Exceptions;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Domain.UnitTests.Rentings;

/// <summary>
/// What becomes of the deposit. The rule that matters is that "not decided" is a
/// state the hire can be in and can be seen to be in — a deposit whose fate
/// nobody recorded is the dispute this exists to prevent.
/// </summary>
public class RentingDepositTests
{
    private static readonly DateTime Start = new(2030, 5, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 5, 5, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SettledAt = new(2030, 5, 5, 17, 0, 0, DateTimeKind.Utc);

    private static Renting AHireWithDeposit(decimal deposit = 300m) =>
        Renting.Create(carId: 1, clientId: 2, startDate: Start, endDate: End,
            price: Money.Of(400m, "TND"), depositAmount: Money.Of(deposit, "TND"));

    private static Renting AHireWithoutDeposit() =>
        Renting.Create(carId: 1, clientId: 2, startDate: Start, endDate: End,
            price: Money.Of(400m, "TND"));

    [Test]
    public void ANewHireWithADeposit_ShouldHaveItUnsettled()
    {
        var renting = AHireWithDeposit();

        renting.HasUnsettledDeposit.Should().BeTrue();
        renting.DepositSettledAt.Should().BeNull();
        renting.DepositRetainedAmount.Should().BeNull();
    }

    [Test]
    public void AHireWithNoDeposit_ShouldHaveNothingToSettle()
    {
        AHireWithoutDeposit().HasUnsettledDeposit.Should().BeFalse();
    }

    [Test]
    public void SettleDeposit_RetainingNothing_ShouldStillCloseTheQuestion()
    {
        var renting = AHireWithDeposit();

        renting.SettleDeposit(Money.Of(0m, "TND"), SettledAt);

        // Zero normalises to null; SettledAt is what says it was answered.
        renting.DepositRetainedAmount.Should().BeNull();
        renting.DepositSettledAt.Should().Be(SettledAt);
        renting.HasUnsettledDeposit.Should().BeFalse();
    }

    [Test]
    public void SettleDeposit_ShouldRecordAPartialRetention()
    {
        var renting = AHireWithDeposit();

        renting.SettleDeposit(Money.Of(120m, "TND"), SettledAt);

        renting.DepositRetainedAmount.Should().Be(Money.Of(120m, "TND"));
        renting.HasUnsettledDeposit.Should().BeFalse();
    }

    [Test]
    public void SettleDeposit_ShouldAllowKeepingTheWholeDeposit()
    {
        var renting = AHireWithDeposit();

        renting.SettleDeposit(Money.Of(300m, "TND"), SettledAt);

        renting.DepositRetainedAmount.Should().Be(Money.Of(300m, "TND"));
    }

    [Test]
    public void SettleDeposit_ShouldRefuseKeepingMoreThanIsHeld()
    {
        var renting = AHireWithDeposit();

        FluentActions.Invoking(() => renting.SettleDeposit(Money.Of(301m, "TND"), SettledAt))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void SettleDeposit_ShouldRefuseANegativeRetention()
    {
        var renting = AHireWithDeposit();

        FluentActions.Invoking(() => renting.SettleDeposit(Money.Of(-1m, "TND"), SettledAt))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void SettleDeposit_ShouldRefuseAnotherCurrency()
    {
        var renting = AHireWithDeposit();

        FluentActions.Invoking(() => renting.SettleDeposit(Money.Of(50m, "EUR"), SettledAt))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void SettleDeposit_OnAHireWithNoDeposit_ShouldBeRefused()
    {
        FluentActions.Invoking(() => AHireWithoutDeposit().SettleDeposit(null, SettledAt))
            .Should().Throw<DomainRuleException>();
    }

    /// <summary>
    /// A hire called off after the deposit was collected still has to say where
    /// that money went, so the cancelled state does not block the settlement.
    /// </summary>
    [Test]
    public void SettleDeposit_ShouldBeAllowedOnACancelledHire()
    {
        var renting = AHireWithDeposit();
        renting.Cancel();

        renting.SettleDeposit(Money.Of(0m, "TND"), SettledAt);

        renting.HasUnsettledDeposit.Should().BeFalse();
    }
}
