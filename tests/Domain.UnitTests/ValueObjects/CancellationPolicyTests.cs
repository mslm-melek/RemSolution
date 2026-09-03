using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Domain.UnitTests.ValueObjects;

/// <summary>
/// What calling a booking off costs. The boundaries matter both ways: a customer
/// charged a day early will dispute it, and one charged nothing an hour before
/// pickup leaves the agency with an empty car.
/// </summary>
public class CancellationPolicyTests
{
    private static readonly DateTime Start = new(2030, 6, 20, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Money Price = Money.Of(300m, "TND");

    // Free more than 48h out, charged inside that, refused inside 24h.
    private static CancellationPolicy APolicy(
        CancellationFeeMode mode = CancellationFeeMode.FixedAmount, decimal value = 45m) =>
        new(mode, value, FreeHours: 48, CutoffHours: 24);

    [Test]
    public void EarlierThanTheFreeWindow_ShouldCostNothing()
    {
        var threeDaysBefore = Start.AddHours(-72);

        APolicy().FeeFor(Price, Start, threeDaysBefore, "TND").Should().BeNull();
    }

    [Test]
    public void InsideTheFreeWindow_ShouldCostTheFee()
    {
        var thirtySixHoursBefore = Start.AddHours(-36);

        var fee = APolicy().FeeFor(Price, Start, thirtySixHoursBefore, "TND");

        fee!.Amount.Should().Be(45m);
        fee.Currency.Should().Be("TND");
    }

    /// <summary>The boundary itself is late — the free window has closed.</summary>
    [Test]
    public void ExactlyOnTheBoundary_ShouldCostTheFee()
    {
        APolicy().FeeFor(Price, Start, Start.AddHours(-48), "TND").Should().NotBeNull();
        APolicy().FeeFor(Price, Start, Start.AddHours(-48).AddSeconds(-1), "TND").Should().BeNull();
    }

    [Test]
    public void APercentage_ShouldBeTakenOfThePrice()
    {
        var policy = new CancellationPolicy(
            CancellationFeeMode.PercentOfPrice, 30m, FreeHours: 48, CutoffHours: 24);

        policy.FeeFor(Price, Start, Start.AddHours(-36), "TND")!.Amount.Should().Be(90m);
    }

    /// <summary>A share of nothing is nothing, not a figure nobody can check.</summary>
    [Test]
    public void APercentageOfAnUnpricedBooking_ShouldCostNothing()
    {
        var policy = new CancellationPolicy(
            CancellationFeeMode.PercentOfPrice, 30m, FreeHours: 48, CutoffHours: 24);

        policy.FeeFor(null, Start, Start.AddHours(-36), "TND").Should().BeNull();
    }

    /// <summary>Calling a booking off cannot cost more than taking it.</summary>
    [Test]
    public void AFeeAboveThePrice_ShouldBeCappedAtThePrice()
    {
        var policy = APolicy(CancellationFeeMode.FixedAmount, 500m);

        policy.FeeFor(Price, Start, Start.AddHours(-36), "TND")!.Amount.Should().Be(300m);
    }

    [Test]
    public void WithFeesOff_ShouldCostNothingHoweverLate()
    {
        var policy = APolicy(CancellationFeeMode.None, 45m);

        policy.ChargesNothing.Should().BeTrue();
        policy.FeeFor(Price, Start, Start.AddHours(-25), "TND").Should().BeNull();
    }

    /// <summary>Nothing is late when nothing is scheduled.</summary>
    [Test]
    public void ABookingWithNoStartDate_ShouldCostNothing()
    {
        APolicy().IsLate(null, Start).Should().BeFalse();
        APolicy().FeeFor(Price, null, Start, "TND").Should().BeNull();
    }
}
