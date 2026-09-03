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

    // --- Derived expiry dates -------------------------------------------------

    private static readonly DateTime Issued = new(2020, 3, 10, 0, 0, 0, DateTimeKind.Utc);

    private static Client AClientWithIssueDates() => new()
    {
        CINDeliveranceDate = Issued,
        PasseportDeliveranceDate = Issued,
        DrivingLicenceDeliveranceDate = Issued,
    };

    [Test]
    public void AMissingExpiry_ShouldBeDerivedFromTheIssueDate()
    {
        var client = AClientWithIssueDates();

        client.ApplyDefaultDocumentExpiries(10, 5, 10);

        client.CINExpiryDate.Should().Be(Issued.AddYears(10));
        client.PasseportExpiryDate.Should().Be(Issued.AddYears(5));
        client.DrivingLicenceExpiryDate.Should().Be(Issued.AddYears(10));
    }

    /// <summary>A date read off the document itself always beats a derived one.</summary>
    [Test]
    public void ARecordedExpiry_ShouldNeverBeOverwritten()
    {
        var onTheDocument = new DateTime(2027, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var client = AClientWithIssueDates();
        client.DrivingLicenceExpiryDate = onTheDocument;

        client.ApplyDefaultDocumentExpiries(10, 5, 10);

        client.DrivingLicenceExpiryDate.Should().Be(onTheDocument);
    }

    /// <summary>
    /// Zero years is how an agency says the document does not expire in its
    /// jurisdiction — it must derive nothing rather than expire it on the day of
    /// issue.
    /// </summary>
    [Test]
    public void AValidityOfZero_ShouldDeriveNothing()
    {
        var client = AClientWithIssueDates();

        client.ApplyDefaultDocumentExpiries(0, 0, 0);

        client.CINExpiryDate.Should().BeNull();
        client.PasseportExpiryDate.Should().BeNull();
        client.DrivingLicenceExpiryDate.Should().BeNull();
    }

    [Test]
    public void NoIssueDate_ShouldDeriveNothing()
    {
        var client = new Client();

        client.ApplyDefaultDocumentExpiries(10, 5, 10);

        client.CINExpiryDate.Should().BeNull();
        client.PasseportExpiryDate.Should().BeNull();
        client.DrivingLicenceExpiryDate.Should().BeNull();
    }
}
