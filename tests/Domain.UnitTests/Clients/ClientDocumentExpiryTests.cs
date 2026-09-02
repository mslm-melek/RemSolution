using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.Entities;

namespace RemSolution.Domain.UnitTests.Clients;

/// <summary>
/// When a licence counts as lapsed. The boundary matters: a hire refused a day
/// early costs the agency a booking, and one allowed a day late costs it the
/// insurance claim.
/// </summary>
public class ClientDocumentExpiryTests
{
    private static readonly DateTime Today = new(2030, 6, 15, 11, 0, 0, DateTimeKind.Utc);

    private static Client AClient(DateTime? licenceExpiry) =>
        new() { DrivingLicenceExpiryDate = licenceExpiry };

    [Test]
    public void ALicenceExpiringToday_ShouldStillBeValid()
    {
        // "Valid until the 15th" is good for the whole of the 15th.
        AClient(new DateTime(2030, 6, 15, 0, 0, 0, DateTimeKind.Utc))
            .IsDrivingLicenceExpiredOn(Today).Should().BeFalse();
    }

    [Test]
    public void ALicenceThatExpiredYesterday_ShouldBeExpired()
    {
        AClient(new DateTime(2030, 6, 14, 0, 0, 0, DateTimeKind.Utc))
            .IsDrivingLicenceExpiredOn(Today).Should().BeTrue();
    }

    [Test]
    public void ALicenceExpiringTomorrow_ShouldBeValid()
    {
        AClient(new DateTime(2030, 6, 16, 0, 0, 0, DateTimeKind.Utc))
            .IsDrivingLicenceExpiredOn(Today).Should().BeFalse();
    }

    /// <summary>
    /// No recorded expiry is not the same as valid, but it is deliberately not
    /// treated as expired either — refusing every client whose paperwork predates
    /// the field would take the agency off the road.
    /// </summary>
    [Test]
    public void ALicenceWithNoRecordedExpiry_ShouldNotCountAsExpired()
    {
        AClient(null).IsDrivingLicenceExpiredOn(Today).Should().BeFalse();
    }

    /// <summary>
    /// The comparison is by DAY, not by instant: the time on either side must not
    /// decide it, or the same licence is expired at 09:00 and valid at 14:00.
    /// </summary>
    [Test]
    public void TheTimeOfDay_ShouldNotDecideIt()
    {
        var expiry = new DateTime(2030, 6, 15, 23, 59, 0, DateTimeKind.Utc);
        var earlyInTheDay = new DateTime(2030, 6, 15, 0, 1, 0, DateTimeKind.Utc);

        AClient(expiry).IsDrivingLicenceExpiredOn(earlyInTheDay).Should().BeFalse();
        AClient(expiry).IsDrivingLicenceExpiredOn(Today).Should().BeFalse();
    }
}
