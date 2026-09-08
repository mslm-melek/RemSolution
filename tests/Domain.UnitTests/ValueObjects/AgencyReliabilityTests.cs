using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Domain.UnitTests.ValueObjects;

/// <summary>
/// The figure customers weigh before booking. It has to be defensible in both
/// directions: an agency shown as unreliable will dispute it, and one shown as
/// perfect after breaking a booking makes the number worthless.
/// </summary>
public class AgencyReliabilityTests
{
    [Test]
    public void WithNothingOnRecord_ShouldHaveNoScore()
    {
        var reliability = new AgencyReliability(0, 0, 0);

        // Null, not 100: a brand-new agency has not earned a perfect record.
        reliability.Score.Should().BeNull();
    }

    [Test]
    public void WithAnUpheldReportAndNoBookings_ShouldStillScore()
    {
        // A report can only exist against a booking, so this shape means the
        // bookings are older than the column that records them — the report is
        // still on record and must count.
        new AgencyReliability(0, 0, 1).Score.Should().Be(85);
    }

    [Test]
    public void WithEveryBookingHonoured_ShouldScoreFull()
    {
        var reliability = new AgencyReliability(12, 0, 0);

        reliability.Score.Should().Be(100);
        reliability.Honoured.Should().Be(12);
    }

    [Test]
    public void EachBrokenBooking_ShouldCostFifteen()
    {
        new AgencyReliability(20, 1, 0).Score.Should().Be(85);
        new AgencyReliability(20, 2, 0).Score.Should().Be(70);
    }

    [Test]
    public void AnUpheldReport_ShouldCostOnTopOfTheCancellation()
    {
        // The same booking cancelled and then found unjustified: two penalties,
        // because the second is the platform's finding and not the fact itself.
        new AgencyReliability(20, 1, 1).Score.Should().Be(70);
    }

    [Test]
    public void ADisastrousRecord_ShouldFloorAtZero()
    {
        new AgencyReliability(10, 9, 4).Score.Should().Be(0);
    }

    [Test]
    public void MoreCancellationsThanBookings_ShouldNotReportNegativeHonoured()
    {
        // Cannot happen from the counts as read, but the denominator is a
        // separate query from the numerator and must not produce "-1 honoured".
        new AgencyReliability(1, 3, 0).Honoured.Should().Be(0);
    }
}
