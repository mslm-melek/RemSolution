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
    public void Create_ShouldKeepTheAddressToWriteTo_TrimmedOrNotAtAll()
    {
        AgencyReport.Create(1, AgencyReportKind.Other, "Late.", Now, rentingId: 9, notifyEmail: " a@b.tn ")
            .NotifyEmail.Should().Be("a@b.tn");
        AgencyReport.Create(1, AgencyReportKind.Other, "Late.", Now, rentingId: 9, notifyEmail: "  ")
            .NotifyEmail.Should().BeNull();
    }

    // The API translates by this key, so it has to name the state.
    [Test]
    public void ASettledReport_ShouldRefuseWithAKeyNamingItsOutcome()
    {
        var report = AReport();
        report.Uphold("Found at fault.", Now, "platform-admin");

        FluentActions.Invoking(() => report.PostFromCustomer("Hello?", Now, "customer"))
            .Should().Throw<DomainRuleException>()
            .Which.MessageKey.Should().Be("AgencyReport.AlreadyUpheld");
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
    public void Withdraw_ShouldCloseItWithoutCostAndKeepTheReason()
    {
        var report = AReport();

        report.Withdraw("  The agency rebooked me.  ", Now, "customer");

        report.Status.Should().Be(AgencyReportStatus.Withdrawn);
        report.CountsAgainstAgency.Should().BeFalse();
        report.ResolutionNote.Should().Be("The agency rebooked me.");
        report.ResolvedAt.Should().Be(Now);
        report.ResolvedByUserId.Should().Be("customer");

        // Also in the thread, where it outlives a reopening.
        var line = report.Messages.Should().ContainSingle().Subject;
        line.Kind.Should().Be(AgencyReportMessageKind.Withdrawal);
        line.Body.Should().Be("The agency rebooked me.");
        report.MessageCount.Should().Be(1);
    }

    [Test]
    public void Withdraw_ShouldRequireAReason()
    {
        FluentActions.Invoking(() => AReport().Withdraw(" ", Now, "customer"))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Reopen_ShouldPutAWithdrawnReportBackInTheQueue()
    {
        var report = AReport();
        report.Withdraw("They promised a refund.", Now, "customer");

        report.Reopen("  The refund never came.  ", Now.AddDays(10), "customer");

        report.Status.Should().Be(AgencyReportStatus.Open);
        report.ResolutionNote.Should().BeNull();
        report.ResolvedAt.Should().BeNull();
        report.ResolvedByUserId.Should().BeNull();
        report.ReopenedAt.Should().Be(Now.AddDays(10));

        // Both events stay readable, in order.
        report.Messages.Select(m => (m.Kind, m.Body)).Should().Equal(
            (AgencyReportMessageKind.Withdrawal, "They promised a refund."),
            (AgencyReportMessageKind.Reopening, "The refund never came."));

        // And it can now be decided as if it had never been dropped.
        report.Uphold("The agency did not keep its word.", Now.AddDays(11), "platform-admin");
        report.Status.Should().Be(AgencyReportStatus.Upheld);
    }

    /// <summary>Once: a complaint dropped and revived at will is a lever, not arbitration.</summary>
    [Test]
    public void Reopen_ShouldOnlyHappenOnce()
    {
        var report = AReport();
        report.Withdraw("Sorted.", Now, "customer");
        report.Reopen("Not sorted.", Now.AddDays(1), "customer");
        report.Withdraw("Sorted after all.", Now.AddDays(2), "customer");

        FluentActions.Invoking(() => report.Reopen("Again.", Now.AddDays(3), "customer"))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Reopen_ShouldRefuseAVerdictAndAnOpenReport()
    {
        var upheld = AReport();
        upheld.Uphold("The agency gave no cause.", Now, "platform-admin");

        FluentActions.Invoking(() => upheld.Reopen("Again.", Now, "customer"))
            .Should().Throw<DomainRuleException>();
        FluentActions.Invoking(() => AReport().Reopen("Again.", Now, "customer"))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Reopen_ShouldCloseWithTheWindow()
    {
        var report = AReport();
        report.Withdraw("Sorted.", Now, "customer");

        AgencyReport.CanReopen(report.Status, report.ReopenedAt, report.ResolvedAt,
            Now.AddDays(AgencyReport.ReportingWindowDays)).Should().BeTrue();

        FluentActions.Invoking(() => report.Reopen("Too late.",
                Now.AddDays(AgencyReport.ReportingWindowDays + 1), "customer"))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Reopen_ShouldRequireAReason()
    {
        var report = AReport();
        report.Withdraw("Sorted.", Now, "customer");

        FluentActions.Invoking(() => report.Reopen("  ", Now, "customer"))
            .Should().Throw<DomainRuleException>();
        report.Status.Should().Be(AgencyReportStatus.Withdrawn);
    }

    /// <summary>A verdict cannot be withdrawn from under the agency it concerned.</summary>
    [Test]
    public void Withdraw_ShouldRefuseASettledReport()
    {
        var report = AReport();
        report.Uphold("The agency gave no cause.", Now, "platform-admin");

        FluentActions.Invoking(() => report.Withdraw("Never mind.", Now, "customer"))
            .Should().Throw<DomainRuleException>();

        report.Status.Should().Be(AgencyReportStatus.Upheld);
    }

    [Test]
    public void Post_ShouldAddToTheThreadInOrder()
    {
        var report = AReport();

        report.PostFromPlatform("Did they offer another car?", Now, "platform-admin");
        report.PostFromCustomer("  No, nothing.  ", Now.AddHours(1), "customer");

        report.Messages.Should().HaveCount(2);
        report.Messages.First().FromPlatform.Should().BeTrue();
        report.Messages.Last().FromPlatform.Should().BeFalse();
        report.Messages.Last().Body.Should().Be("No, nothing.");
        report.Messages.Last().AuthorUserId.Should().Be("customer");
        // A message is an update of the report row, so its RowVersion guards it.
        report.MessageCount.Should().Be(2);
    }

    [Test]
    public void Post_ShouldRefuseAnEmptyMessage()
    {
        FluentActions.Invoking(() => AReport().PostFromCustomer("  ", Now, "customer"))
            .Should().Throw<DomainRuleException>();
    }

    /// <summary>Frozen once closed: a message after the verdict would read as an appeal.</summary>
    [Test]
    public void Post_ShouldRefuseAClosedReport()
    {
        var dismissed = AReport();
        dismissed.Dismiss("Not the agency's doing.", Now, "platform-admin");

        var withdrawn = AReport();
        withdrawn.Withdraw("Sorted out.", Now, "customer");

        FluentActions.Invoking(() => dismissed.PostFromCustomer("But…", Now, "customer"))
            .Should().Throw<DomainRuleException>();
        FluentActions.Invoking(() => withdrawn.PostFromPlatform("Are you sure?", Now, "platform-admin"))
            .Should().Throw<DomainRuleException>();
    }

    /// <summary>
    /// Free text is where a phone number ends up, so the customer's follow-ups
    /// go; the opening account and the outcome stay as the record of the
    /// agency's conduct.
    /// </summary>
    [Test]
    public void ErasePersonalData_ShouldEmptyTheCustomersFollowUpsButKeepTheComplaint()
    {
        var report = AgencyReport.Create(1, AgencyReportKind.CancelledBooking,
            "They cancelled the morning of the pickup.", Now,
            reservationId: 7, reporterUserId: "customer", reporterName: "Jane Doe",
            notifyEmail: "jane@example.com");
        report.CreatedBy = "customer";
        report.PostFromCustomer("Call me on 22 000 000.", Now, "customer");
        report.PostFromPlatform("Noted.", Now, "platform-admin");
        report.Withdraw("Sorted it out with them, call me on 22 000 000.", Now, "customer");

        report.ErasePersonalData();

        report.ReporterName.Should().BeNull();
        report.ReporterUserId.Should().BeNull();
        report.NotifyEmail.Should().BeNull();
        report.CreatedBy.Should().BeNull();
        report.ResolvedByUserId.Should().BeNull();
        // A withdrawal's outcome is their own words, unlike a verdict's.
        report.ResolutionNote.Should().BeNull();
        report.Status.Should().Be(AgencyReportStatus.Withdrawn);

        report.Messages.Where(m => !m.FromPlatform).Should().AllSatisfy(m =>
        {
            m.Body.Should().BeEmpty();
            m.AuthorUserId.Should().BeNull();
        });

        var platform = report.Messages.Single(m => m.FromPlatform);
        platform.Body.Should().Be("Noted.");
        platform.AuthorUserId.Should().Be("platform-admin");

        report.Message.Should().Be("They cancelled the morning of the pickup.");
    }

    [Test]
    public void ErasePersonalData_ShouldKeepAVerdictsNote()
    {
        var report = AReport();
        report.Dismiss("The car was written off.", Now, "platform-admin");

        report.ErasePersonalData();

        report.ResolutionNote.Should().Be("The car was written off.");
    }

    /// <summary>A refused withdrawal must not leave the report half-closed.</summary>
    [Test]
    public void Withdraw_ShouldChangeNothingWhenTheReasonIsRefused()
    {
        var report = AReport();

        FluentActions.Invoking(() => report.Withdraw(
                new string('x', AgencyReport.MaxMessageLength + 1), Now, "customer"))
            .Should().Throw<DomainRuleException>();

        report.Status.Should().Be(AgencyReportStatus.Open);
        report.ResolutionNote.Should().BeNull();
        report.Messages.Should().BeEmpty();
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
