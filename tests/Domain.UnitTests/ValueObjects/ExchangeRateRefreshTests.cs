using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Domain.UnitTests.ValueObjects;

/// <summary>
/// Whether a rate from the automatic feed is worth believing. It lands unattended
/// on the public marketplace, so both directions matter: refusing a real move
/// costs a stale second line, accepting a garbage one prices everything wrong.
/// </summary>
public class ExchangeRateRefreshTests
{
    // 1 TND = 0.296 EUR, roughly.
    private const decimal Current = 0.296m;

    [Test]
    public void AnOrdinaryDailyMove_ShouldBeAccepted()
    {
        ExchangeRateRefresh.IsPlausible(Current, 0.2985m).Should().BeTrue();
        ExchangeRateRefresh.IsPlausible(Current, 0.2930m).Should().BeTrue();
    }

    [Test]
    public void AMoveExactlyOnTheBound_ShouldBeAccepted()
    {
        // Inclusive on purpose: the bound is a guard against nonsense, not a
        // judgement about what a currency may do.
        ExchangeRateRefresh.IsPlausible(100m, 125m).Should().BeTrue();
        ExchangeRateRefresh.IsPlausible(100m, 75m).Should().BeTrue();
    }

    [Test]
    public void AMoveBeyondTheBound_ShouldBeRefused()
    {
        ExchangeRateRefresh.IsPlausible(100m, 125.01m).Should().BeFalse();
        ExchangeRateRefresh.IsPlausible(100m, 74.99m).Should().BeFalse();
    }

    // The failure this exists for: a feed that changes its base or its scale
    // answers with a plausible-looking number for the wrong pair.
    [Test]
    public void AFigureFromAnotherCurrencyAltogether_ShouldBeRefused()
    {
        // 1 TND = 0.296 EUR one day and 3.23 the next is the MAD rate, not a
        // devaluation.
        ExchangeRateRefresh.IsPlausible(Current, 3.23m).Should().BeFalse();
    }

    [Test]
    public void ANonPositiveCandidate_ShouldNeverBeAccepted()
    {
        ExchangeRateRefresh.IsPlausible(Current, 0m).Should().BeFalse();
        ExchangeRateRefresh.IsPlausible(Current, -0.296m).Should().BeFalse();
    }

    [Test]
    public void WithNothingToCompareAgainst_ShouldAcceptAnyPositiveRate()
    {
        // There is no stored rate to be a multiple of, so the only check left is
        // the one the entity already makes.
        ExchangeRateRefresh.IsPlausible(0m, 3.23m).Should().BeTrue();
    }
}
