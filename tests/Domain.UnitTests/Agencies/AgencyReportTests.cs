using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.Exceptions;

namespace RemSolution.Domain.UnitTests.Agencies;

/// <summary>
/// The complaint a customer takes to the platform. An arbitration that could be
/// re-run, or a verdict with no reasoning, is not arbitration — so those are the
/// rules worth asserting.
/// </summary>
public class AgencyReportTests
{
    private static readonly DateTime Now = new(2030, 7, 10, 12, 0, 0, DateTimeKind.Utc);

    private static AgencyReport AReport() =>
        AgencyReport.Create(
            agencyId: 1,
            kind: AgencyReportKind.CancelledBooking,
            message: "They cancelled the morning of the pickup.",
            at: Now,
            reservationId: 7);

    [Test]
    public void Create_ShouldOpenAWaitingReport()
    {
        var report = AReport();

        report.Status.Should().Be(AgencyReportStatus.Open);
        report.ReservationId.Should().Be(7);
        report.RentingId.Should().BeNull();
        report.SubmittedAt.Should().Be(Now);
        report.CountsAgainstAgency.Should().BeFalse();
    }

    [Test]
    public void Create_ShouldRequireExactlyOneBooking()
    {
        FluentActions.Invoking(() => AgencyReport.Create(
                1, AgencyReportKind.Other, "Something", Now))
            .Should().Throw<DomainRuleException>();

        FluentActions.Invoking(() => AgencyReport.Create(
                1, AgencyReportKind.Other, "Something", Now, reservationId: 7, rentingId: 9))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Create_ShouldRefuseAnEmptyAccount()
    {
        FluentActions.Invoking(() => AgencyReport.Create(
                1, AgencyReportKind.Other, "   ", Now, rentingId: 9))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Uphold_ShouldSettleItAgainstTheAgency()
    {
        var report = AReport();

        report.Uphold("The agency gave no cause.", Now, "platform-admin");

        report.Status.Should().Be(AgencyReportStatus.Upheld);
        report.CountsAgainstAgency.Should().BeTrue();
        report.ResolvedAt.Should().Be(Now);
        report.ResolvedByUserId.Should().Be("platform-admin");
    }

    [Test]
    public void Dismiss_ShouldSettleItWithoutCost()
    {
        var report = AReport();

        report.Dismiss("The car was written off; the agency rebooked them.", Now, "platform-admin");

        report.Status.Should().Be(AgencyReportStatus.Dismissed);
        report.CountsAgainstAgency.Should().BeFalse();
    }

    [Test]
    public void Resolve_ShouldRequireReasoning()
    {
        FluentActions.Invoking(() => AReport().Uphold("  ", Now, "platform-admin"))
            .Should().Throw<DomainRuleException>();
    }

    /// <summary>Settled once — or one complaint could move a score twice.</summary>
    [Test]
    public void Resolve_ShouldRefuseASecondVerdict()
    {
        var report = AReport();
        report.Dismiss("Not the agency's doing.", Now, "platform-admin");

        FluentActions.Invoking(() => report.Uphold("Changed my mind.", Now, "platform-admin"))
            .Should().Throw<DomainRuleException>();

        report.Status.Should().Be(AgencyReportStatus.Dismissed);
    }

    [Test]
    public void CanReport_ShouldOnlyAllowAHoldTheAgencyConfirmed()
    {
        AgencyReport.CanReport(ReservationStatus.Confirmed, false).Should().BeTrue();
        AgencyReport.CanReport(ReservationStatus.Paid, false).Should().BeTrue();
        AgencyReport.CanReport(ReservationStatus.Cancelled, true).Should().BeTrue();

        // Never confirmed: nothing was promised, so there is nothing to answer for.
        AgencyReport.CanReport(ReservationStatus.PendingConfirmation, false).Should().BeFalse();
        AgencyReport.CanReport(ReservationStatus.Rejected, false).Should().BeFalse();
        AgencyReport.CanReport(ReservationStatus.Expired, false).Should().BeFalse();
        AgencyReport.CanReport(ReservationStatus.Cancelled, false).Should().BeFalse();

        // Converted: it became a hire, and the hire carries the report. Leaving it
        // reportable here would let one rental be complained about twice — once on
        // the hold, once on the hire — and cost two upheld reports.
        AgencyReport.CanReport(ReservationStatus.Converted, false).Should().BeFalse();
    }

    [Test]
    public void ReportingWindow_ShouldCloseAfterTheBooking()
    {
        var ended = new DateTime(2030, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        AgencyReport.IsWithinReportingWindow(ended, ended.AddDays(59)).Should().BeTrue();
        AgencyReport.IsWithinReportingWindow(ended, ended.AddDays(61)).Should().BeFalse();

        // Nothing to count from: refusing on a date nobody recorded is arbitrary.
        AgencyReport.IsWithinReportingWindow(null, ended.AddYears(5)).Should().BeTrue();
    }
}
