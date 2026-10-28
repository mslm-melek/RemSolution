using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Agency.Queries.GetMyAgencyReliabilityQuery;
using RemSolution.Application.Features.AgencyReport.Commands.PostAgencyReportMessageCommand;
using RemSolution.Application.Features.AgencyReport.Commands.ResolveAgencyReportCommand;
using RemSolution.Application.Features.AgencyReport.Queries.GetAgencyReportsQuery;
using RemSolution.Application.Features.AgencyReport.Queries.GetMyAgencyReportsQuery;
using RemSolution.Application.Features.Marketplace.Commands.CancelMyReservationCommand;
using RemSolution.Application.Features.Marketplace.Commands.CreateCustomerReservationCommand;
using RemSolution.Application.Features.Marketplace.Commands.CreateMyReportCommand;
using RemSolution.Application.Features.Marketplace.Commands.PostMyReportMessageCommand;
using RemSolution.Application.Features.Marketplace.Commands.ReopenMyReportCommand;
using RemSolution.Application.Features.Marketplace.Commands.WithdrawMyReportCommand;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMarketplaceAgencyQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyReportsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyReservationsQuery;
using RemSolution.Application.Features.Notification.Commands.MarkNotificationsReadCommand;
using RemSolution.Application.Features.Notification.Queries.GetMyNotificationsQuery;
using RemSolution.Application.Features.Notification.Queries.GetMyUnreadNotificationCountQuery;
using RemSolution.Application.Features.Reservation.Commands.CancelReservationCommand;
using RemSolution.Application.Features.Reservation.Commands.ConfirmReservationCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using RemSolution.Infrastructure.Data;
using RemSolution.Infrastructure.Identity;
// Not a using for the namespace: it also holds a NotFoundException and a
// ForbiddenAccessException, both of which the Application layer names too.
using DomainRuleException = RemSolution.Domain.Exceptions.DomainRuleException;

namespace RemSolution.Application.FunctionalTests.Marketplace;

using static Testing;

/// <summary>
/// An agency that calls off a booking it had confirmed, the complaint that can
/// follow, and what the platform's verdict does to the figure customers see.
/// </summary>
public class AgencyReportTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 3, 5, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Dob = new(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    // --------------------------------------------------- cancelling a promise ---

    [Test]
    public async Task CancellingAConfirmedHoldRequiresAReason()
    {
        var booking = await ConfirmedBookingAsync("rep1@local");

        BeTheAgency(booking);

        await FluentActions.Invoking(() => SendAsync(new CancelReservationCommand(booking.ReservationId)))
            .Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task CancellingAPendingRequestStillNeedsNoReason()
    {
        var booking = await PendingBookingAsync("rep2@local");

        BeTheAgency(booking);
        await SendAsync(new CancelReservationCommand(booking.ReservationId));

        var reservation = await FindIgnoringFiltersAsync<Reservation>(r => r.Id == booking.ReservationId);
        reservation!.Status.Should().Be(ReservationStatus.Cancelled);
        // Nothing was ever promised, so nothing counts against the agency.
        reservation.CancelledAfterConfirmation.Should().BeFalse();
    }

    [Test]
    public async Task CancellingAConfirmedHoldCountsAgainstTheAgency()
    {
        var booking = await ConfirmedBookingAsync("rep3@local");

        await CancelAsAgencyAsync(booking);

        BeTheCustomer(booking);
        var shopfront = await SendAsync(new GetMarketplaceAgencyQuery(booking.AgencyId));

        shopfront!.Reliability.ConfirmedBookings.Should().Be(1);
        shopfront.Reliability.CancelledByAgency.Should().Be(1);
        shopfront.Reliability.Honoured.Should().Be(0);
        shopfront.Reliability.Score.Should().Be(85);
    }

    [Test]
    public async Task AnAgencyWithNoConfirmedBookingHasNoScoreRatherThanAPerfectOne()
    {
        var booking = await PendingBookingAsync("rep4@local");

        BeTheCustomer(booking);
        var shopfront = await SendAsync(new GetMarketplaceAgencyQuery(booking.AgencyId));

        shopfront!.Reliability.ConfirmedBookings.Should().Be(0);
        shopfront.Reliability.Score.Should().BeNull();
    }

    [Test]
    public async Task AHoldTheCustomerCancelledDoesNotCountAgainstTheAgency()
    {
        var booking = await ConfirmedBookingAsync("rep5@local");

        BeTheCustomer(booking);
        await SendAsync(new CancelMyReservationCommand(booking.ReservationId, "Plans changed."));

        var shopfront = await SendAsync(new GetMarketplaceAgencyQuery(booking.AgencyId));

        shopfront!.Reliability.ConfirmedBookings.Should().Be(1);
        shopfront.Reliability.CancelledByAgency.Should().Be(0);
        shopfront.Reliability.Score.Should().Be(100);
    }

    // ------------------------------------------------------- raising a report ---

    [Test]
    public async Task CustomerCanReportABookingTheAgencyCancelled()
    {
        var booking = await ConfirmedBookingAsync("rep6@local");
        await CancelAsAgencyAsync(booking, "Our car was written off.");

        BeTheCustomer(booking);
        var reportId = await SendAsync(new CreateMyReportCommand
        {
            ReservationId = booking.ReservationId,
            Kind = AgencyReportKind.CancelledBooking,
            Message = "  They cancelled the day before and offered nothing.  "
        });

        var report = await FindIgnoringFiltersAsync<AgencyReport>(r => r.Id == reportId);
        report!.AgencyId.Should().Be(booking.AgencyId);
        report.Status.Should().Be(AgencyReportStatus.Open);
        report.Message.Should().Be("They cancelled the day before and offered nothing."); // trimmed
        report.ReporterUserId.Should().Be(booking.CustomerId);
        report.ReporterName.Should().Be("Jane Doe");
        // Snapshotted, because the platform administrator reading it has no
        // tenant claim and cannot join back to the booking.
        report.BookingSummary.Should().Contain("2030-03-01");
        report.AgencyCancellationReason.Should().Be("Our car was written off.");
    }

    [Test]
    public async Task ARequestTheAgencyNeverConfirmedCannotBeReported()
    {
        var booking = await PendingBookingAsync("rep7@local");

        BeTheCustomer(booking);

        await FluentActions.Invoking(() => SendAsync(new CreateMyReportCommand
        {
            ReservationId = booking.ReservationId,
            Kind = AgencyReportKind.Other,
            Message = "They never answered."
        })).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task TheCancelledBookingReasonIsOnlyAvailableWhenTheAgencyCancelled()
    {
        var booking = await ConfirmedBookingAsync("rep8@local");

        BeTheCustomer(booking);

        await FluentActions.Invoking(() => SendAsync(new CreateMyReportCommand
        {
            ReservationId = booking.ReservationId,
            Kind = AgencyReportKind.CancelledBooking,
            Message = "They cancelled."
        })).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ABookingCanOnlyBeReportedOnce()
    {
        var booking = await ConfirmedBookingAsync("rep9@local");
        await CancelAsAgencyAsync(booking);

        BeTheCustomer(booking);
        await SendAsync(new CreateMyReportCommand
        {
            ReservationId = booking.ReservationId,
            Kind = AgencyReportKind.CancelledBooking,
            Message = "First complaint."
        });

        await FluentActions.Invoking(() => SendAsync(new CreateMyReportCommand
        {
            ReservationId = booking.ReservationId,
            Kind = AgencyReportKind.Other,
            Message = "Second complaint."
        })).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ACustomerCannotReportSomeoneElsesBooking()
    {
        var booking = await ConfirmedBookingAsync("rep10@local");
        await CancelAsAgencyAsync(booking);

        // A different marketplace account: someone else's booking is
        // indistinguishable from one that does not exist.
        await RunAsUserAsync("stranger-rep@local", "Customer1234!", new[] { Roles.Customer });
        SetCurrentAgency(null);

        await FluentActions.Invoking(() => SendAsync(new CreateMyReportCommand
        {
            ReservationId = booking.ReservationId,
            Kind = AgencyReportKind.Other,
            Message = "Not mine."
        })).Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task MyReservationsSaysWhetherABookingCanStillBeReported()
    {
        var booking = await ConfirmedBookingAsync("rep11@local");
        await CancelAsAgencyAsync(booking);

        BeTheCustomer(booking);
        var before = await SendAsync(new GetMyReservationsQuery());
        before.Should().HaveCount(1);
        before[0].CancelledByAgency.Should().BeTrue();
        before[0].CanReport.Should().BeTrue();
        before[0].MyReportId.Should().BeNull();

        var reportId = await SendAsync(new CreateMyReportCommand
        {
            ReservationId = booking.ReservationId,
            Kind = AgencyReportKind.CancelledBooking,
            Message = "Left me stranded."
        });

        var after = await SendAsync(new GetMyReservationsQuery());
        after[0].CanReport.Should().BeFalse();
        after[0].MyReportId.Should().Be(reportId);
        after[0].MyReportStatus.Should().Be(AgencyReportStatus.Open);
    }

    // ----------------------------------------------------------- arbitrating ---

    [Test]
    public async Task UpholdingAReportCostsTheAgencyMorePoints()
    {
        var booking = await ConfirmedBookingAsync("rep12@local");
        await CancelAsAgencyAsync(booking);

        BeTheCustomer(booking);
        var reportId = await SendAsync(new CreateMyReportCommand
        {
            ReservationId = booking.ReservationId,
            Kind = AgencyReportKind.CancelledBooking,
            Message = "No cause given."
        });

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null); // a platform administrator carries no tenant
        await SendAsync(new ResolveAgencyReportCommand(reportId, true, "The agency gave no cause."));

        var shopfront = await SendAsync(new GetMarketplaceAgencyQuery(booking.AgencyId));

        // 15 for breaking the booking, 15 more for being found at fault.
        shopfront!.Reliability.UpheldReports.Should().Be(1);
        shopfront.Reliability.Score.Should().Be(70);
    }

    [Test]
    public async Task DismissingAReportCostsNothingExtra()
    {
        var booking = await ConfirmedBookingAsync("rep13@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking);

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);
        await SendAsync(new ResolveAgencyReportCommand(reportId, false, "The car was written off."));

        var shopfront = await SendAsync(new GetMarketplaceAgencyQuery(booking.AgencyId));

        shopfront!.Reliability.UpheldReports.Should().Be(0);
        shopfront.Reliability.Score.Should().Be(85); // the cancellation alone
    }

    [Test]
    public async Task AReportIsSettledOnce()
    {
        var booking = await ConfirmedBookingAsync("rep14@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking);

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);
        await SendAsync(new ResolveAgencyReportCommand(reportId, false, "Nothing in it."));

        await FluentActions.Invoking(() =>
                SendAsync(new ResolveAgencyReportCommand(reportId, true, "Changed my mind.")))
            .Should().ThrowAsync<DomainRuleException>();
    }

    [Test]
    public async Task AVerdictNeedsReasoning()
    {
        var booking = await ConfirmedBookingAsync("rep15@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking);

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);

        await FluentActions.Invoking(() =>
                SendAsync(new ResolveAgencyReportCommand(reportId, true, "   ")))
            .Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task OnlyThePlatformSettlesAReport()
    {
        var booking = await ConfirmedBookingAsync("rep16@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking);

        BeTheAgency(booking);

        await FluentActions.Invoking(() =>
                SendAsync(new ResolveAgencyReportCommand(reportId, false, "Not our fault.")))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    // ------------------------------------------------------------- who reads ---

    [Test]
    public async Task TheTriageQueueCarriesBothAccounts()
    {
        var booking = await ConfirmedBookingAsync("rep17@local");
        await CancelAsAgencyAsync(booking, "Mechanical failure.");
        await ReportAsCustomerAsync(booking, "Told an hour before pickup.");

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);

        var page = await SendAsync(new GetAgencyReportsQuery(AgencyReportStatus.Open));

        page.Items.Should().ContainSingle();
        var report = page.Items.Single();
        report.AgencyName.Should().Be("Test Agency");
        report.AgencyCancellationReason.Should().Be("Mechanical failure.");
        report.Message.Should().Be("Told an hour before pickup.");
    }

    [Test]
    public async Task AnAgencySeesTheComplaintsAgainstItAndItsOwnScore()
    {
        var booking = await ConfirmedBookingAsync("rep18@local");
        await CancelAsAgencyAsync(booking);
        await ReportAsCustomerAsync(booking, "Nothing offered in its place.");

        BeTheAgency(booking);

        var reports = await SendAsync(new GetMyAgencyReportsQuery());
        reports.Items.Should().ContainSingle()
            .Which.Message.Should().Be("Nothing offered in its place.");

        var reliability = await SendAsync(new GetMyAgencyReliabilityQuery());
        // The same arithmetic the shopfront shows, read from the agency's own
        // (tenant-filtered) rows.
        reliability.CancelledByAgency.Should().Be(1);
        reliability.Score.Should().Be(85);
    }

    [Test]
    public async Task ACustomerSeesTheVerdictOnTheirOwnReport()
    {
        var booking = await ConfirmedBookingAsync("rep19@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking);

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);
        await SendAsync(new ResolveAgencyReportCommand(reportId, true, "The agency was in the wrong."));

        BeTheCustomer(booking);
        var mine = await SendAsync(new GetMyReportsQuery());

        mine.Should().ContainSingle();
        mine[0].Status.Should().Be(AgencyReportStatus.Upheld);
        mine[0].ResolutionNote.Should().Be("The agency was in the wrong.");
        mine[0].AgencyName.Should().Be("Test Agency");
    }

    // ------------------------------------------------- the thread, and closing ---

    [Test]
    public async Task TheCustomerAndThePlatformWriteOnAnOpenReportAndTheAgencyReadsIt()
    {
        var booking = await ConfirmedBookingAsync("rep20@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking);

        var platformAdminId = await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);
        await SendAsync(new PostAgencyReportMessageCommand(reportId, "Were you offered another car?"));

        BeTheCustomer(booking);
        var waitingOnMe = await SendAsync(new GetMyReportsQuery());
        waitingOnMe[0].LastMessageFromPlatform.Should().BeTrue();

        await SendAsync(new PostMyReportMessageCommand(reportId, "No, nothing at all."));

        var mine = await SendAsync(new GetMyReportsQuery());
        mine[0].Messages.Select(m => (m.FromPlatform, m.Mine, m.Body)).Should().Equal(
            (true, false, "Were you offered another car?"),
            (false, true, "No, nothing at all."));
        mine[0].LastMessageFromPlatform.Should().BeFalse();

        SetCurrentUser(platformAdminId); // already created above
        SetCurrentAgency(null);
        var queue = await SendAsync(new GetAgencyReportsQuery(AgencyReportStatus.Open));
        // The customer wrote last: the platform owes an answer.
        queue.Items.Single().LastMessageFromPlatform.Should().BeFalse();
        queue.Items.Single().Messages.Select(m => m.Mine).Should().Equal(true, false);

        BeTheAgency(booking);
        var theirs = await SendAsync(new GetMyAgencyReportsQuery());
        theirs.Items.Single().Messages.Should().HaveCount(2)
            .And.OnlyContain(m => !m.Mine, "the agency is not a party to the thread");
    }

    [Test]
    public async Task AnotherPlatformAdministratorsMessageIsNotMine()
    {
        var booking = await ConfirmedBookingAsync("rep27@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking);

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);
        await SendAsync(new PostAgencyReportMessageCommand(reportId, "Do you have a receipt?"));

        await RunAsUserAsync("second-admin@local", "PlatformAdmin1234!", new[] { Roles.PlatformAdministrator });
        SetCurrentAgency(null);

        var queue = await SendAsync(new GetAgencyReportsQuery(AgencyReportStatus.Open));
        var line = queue.Items.Single().Messages.Single();
        line.FromPlatform.Should().BeTrue();
        line.Mine.Should().BeFalse();
    }

    [Test]
    public async Task ACustomerCanReopenAWithdrawalOnceAndThePlatformThenDecidesIt()
    {
        var booking = await ConfirmedBookingAsync("rep28@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking);

        BeTheCustomer(booking);
        await SendAsync(new WithdrawMyReportCommand(reportId, "They promised a refund."));
        (await SendAsync(new GetMyReportsQuery()))[0].CanReopen.Should().BeTrue();

        await SendAsync(new ReopenMyReportCommand(reportId, "The refund never came."));

        var mine = await SendAsync(new GetMyReportsQuery());
        mine[0].Status.Should().Be(AgencyReportStatus.Open);
        mine[0].ResolutionNote.Should().BeNull();
        mine[0].CanReopen.Should().BeFalse();
        mine[0].Messages.Select(m => m.Kind).Should().Equal(
            AgencyReportMessageKind.Withdrawal, AgencyReportMessageKind.Reopening);

        // Once only.
        await SendAsync(new WithdrawMyReportCommand(reportId, "Refunded after all."));
        (await SendAsync(new GetMyReportsQuery()))[0].CanReopen.Should().BeFalse();
        await FluentActions.Invoking(() => SendAsync(new ReopenMyReportCommand(reportId, "Again.")))
            .Should().ThrowAsync<DomainRuleException>();
    }

    [Test]
    public async Task AReopenedReportCanBeUpheld()
    {
        var booking = await ConfirmedBookingAsync("rep29@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking);

        BeTheCustomer(booking);
        await SendAsync(new WithdrawMyReportCommand(reportId, "They promised a refund."));
        await SendAsync(new ReopenMyReportCommand(reportId, "The refund never came."));

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);
        await SendAsync(new ResolveAgencyReportCommand(reportId, true, "The agency did not keep its word."));

        var shopfront = await SendAsync(new GetMarketplaceAgencyQuery(booking.AgencyId));
        shopfront!.Reliability.UpheldReports.Should().Be(1);
    }

    [Test]
    public async Task ReopeningTellsTheAgency()
    {
        var booking = await ConfirmedBookingAsync("rep30@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking);

        // The harness creates users with no agency; the recipient lookup asks the
        // Identity store who works for the agency, so the column has to be set.
        await UsingScopeAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = await users.FindByIdAsync(booking.AgencyAdminId);
            admin!.AgencyId = booking.AgencyId;
            await users.UpdateAsync(admin);
            return true;
        });

        BeTheCustomer(booking);
        await SendAsync(new WithdrawMyReportCommand(reportId, "Sorted."));
        await SendAsync(new ReopenMyReportCommand(reportId, "Not sorted."));

        BeTheAgency(booking); // notifications are tenant rows
        var alerts = (await AllAsync<Notification>())
            .Where(n => n.SubjectId == reportId && n.AgencyId == booking.AgencyId)
            .Select(n => n.MessageKey)
            .ToList();

        alerts.Should().Contain("agencyReportWithdrawn").And.Contain("agencyReportReopened");
    }

    /// <summary>
    /// The race the row version exists for: both sides read the report open,
    /// the platform decides first, and the customer's message must then fail
    /// rather than land on a closed report.
    /// </summary>
    [Test]
    public async Task AMessageRacingAVerdictLosesWithAConcurrencyError()
    {
        var booking = await ConfirmedBookingAsync("rep32@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking);

        // Two contexts, as two requests would have.
        await UsingScopeAsync(async customerServices =>
        {
            var customerContext = customerServices.GetRequiredService<ApplicationDbContext>();
            var seenByCustomer = await customerContext.AgencyReports.SingleAsync(r => r.Id == reportId);

            await UsingScopeAsync(async platformServices =>
            {
                var platformContext = platformServices.GetRequiredService<ApplicationDbContext>();
                var seenByPlatform = await platformContext.AgencyReports.SingleAsync(r => r.Id == reportId);
                seenByPlatform.Dismiss("Nothing in it.", DateTime.UtcNow, "platform-admin");
                return await platformContext.SaveChangesAsync();
            });

            // Still Open in the customer's copy, so the entity lets it through —
            // only the row version stands between this and a closed report.
            seenByCustomer.PostFromCustomer("One more thing.", DateTime.UtcNow, booking.CustomerId);

            await FluentActions.Invoking(() => customerContext.SaveChangesAsync())
                .Should().ThrowAsync<DbUpdateConcurrencyException>();

            return true;
        });

        (await AllAsync<AgencyReportMessage>()).Should().NotContain(m => m.AgencyReportId == reportId);
    }

    [Test]
    public async Task AWithdrawalOrReopeningIsNotAReplyToThePlatform()
    {
        var booking = await ConfirmedBookingAsync("rep33@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking);

        var platformAdminId = await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);
        await SendAsync(new PostAgencyReportMessageCommand(reportId, "Do you have a receipt?"));

        BeTheCustomer(booking);
        await SendAsync(new WithdrawMyReportCommand(reportId, "Sorted."));
        await SendAsync(new ReopenMyReportCommand(reportId, "Not sorted."));

        // The platform's question is still the last thing anybody asked.
        (await SendAsync(new GetMyReportsQuery()))[0].LastMessageFromPlatform.Should().BeTrue();

        SetCurrentUser(platformAdminId);
        SetCurrentAgency(null);
        var queue = await SendAsync(new GetAgencyReportsQuery(AgencyReportStatus.Open));
        queue.Items.Single().LastMessageFromPlatform.Should().BeTrue("no reply has come in");
    }

    [Test]
    public async Task ACustomerCannotReopenSomeoneElsesReport()
    {
        var booking = await ConfirmedBookingAsync("rep31@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking);

        BeTheCustomer(booking);
        await SendAsync(new WithdrawMyReportCommand(reportId, "Sorted."));

        await RunAsUserAsync("stranger-rep31@local", "Customer1234!", new[] { Roles.Customer });
        SetCurrentAgency(null);

        await FluentActions.Invoking(() => SendAsync(new ReopenMyReportCommand(reportId, "Mine now.")))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ACustomerCannotWriteOnSomeoneElsesReport()
    {
        var booking = await ConfirmedBookingAsync("rep21@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking);

        await RunAsUserAsync("stranger-rep21@local", "Customer1234!", new[] { Roles.Customer });
        SetCurrentAgency(null);

        await FluentActions.Invoking(() => SendAsync(new PostMyReportMessageCommand(reportId, "Me too.")))
            .Should().ThrowAsync<NotFoundException>();
        await FluentActions.Invoking(() => SendAsync(new WithdrawMyReportCommand(reportId, "Closing it.")))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task TheAgencyIsNotAPartyToTheThread()
    {
        var booking = await ConfirmedBookingAsync("rep22@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking);

        BeTheAgency(booking);

        await FluentActions.Invoking(() =>
                SendAsync(new PostAgencyReportMessageCommand(reportId, "It was not our fault.")))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task ACustomerCanWithdrawTheirReportWithAReasonAtNoCostToTheAgency()
    {
        var booking = await ConfirmedBookingAsync("rep23@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking);

        BeTheCustomer(booking);
        await SendAsync(new WithdrawMyReportCommand(reportId, "The agency refunded me and apologised."));

        var mine = await SendAsync(new GetMyReportsQuery());
        mine[0].Status.Should().Be(AgencyReportStatus.Withdrawn);
        mine[0].ResolutionNote.Should().Be("The agency refunded me and apologised.");

        var shopfront = await SendAsync(new GetMarketplaceAgencyQuery(booking.AgencyId));
        shopfront!.Reliability.UpheldReports.Should().Be(0);
        shopfront.Reliability.Score.Should().Be(85); // the cancellation alone
    }

    [Test]
    public async Task WithdrawingNeedsAReason()
    {
        var booking = await ConfirmedBookingAsync("rep24@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking);

        BeTheCustomer(booking);

        await FluentActions.Invoking(() => SendAsync(new WithdrawMyReportCommand(reportId, "  ")))
            .Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ASettledReportIsFrozen()
    {
        var booking = await ConfirmedBookingAsync("rep25@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking);

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);
        await SendAsync(new ResolveAgencyReportCommand(reportId, false, "The car was written off."));

        await FluentActions.Invoking(() =>
                SendAsync(new PostAgencyReportMessageCommand(reportId, "One more thing.")))
            .Should().ThrowAsync<DomainRuleException>();

        BeTheCustomer(booking);

        await FluentActions.Invoking(() => SendAsync(new PostMyReportMessageCommand(reportId, "I disagree.")))
            .Should().ThrowAsync<DomainRuleException>();
        await FluentActions.Invoking(() => SendAsync(new WithdrawMyReportCommand(reportId, "Never mind.")))
            .Should().ThrowAsync<DomainRuleException>();
    }

    [Test]
    public async Task AWithdrawnReportCannotThenBeArbitrated()
    {
        var booking = await ConfirmedBookingAsync("rep26@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking);

        BeTheCustomer(booking);
        await SendAsync(new WithdrawMyReportCommand(reportId, "Sorted out with the agency."));

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);

        await FluentActions.Invoking(() =>
                SendAsync(new ResolveAgencyReportCommand(reportId, true, "Upholding anyway.")))
            .Should().ThrowAsync<DomainRuleException>();
    }

    // ------------------------------------------------------- who else is told ---

    [Test]
    public async Task ThePlatformIsToldInTheAppWhenTheCustomerActs()
    {
        var booking = await ConfirmedBookingAsync("rep40@local");
        await CancelAsAgencyAsync(booking);

        // The administrator exists before anything happens, as on a real platform.
        var platformAdminId = await RunAsPlatformAdministratorAsync();

        var reportId = await ReportAsCustomerAsync(booking);
        await SendAsync(new PostMyReportMessageCommand(reportId, "Here is the receipt."));
        await SendAsync(new WithdrawMyReportCommand(reportId, "They refunded me."));
        await SendAsync(new ReopenMyReportCommand(reportId, "The refund bounced."));

        SetCurrentUser(platformAdminId);
        SetCurrentAgency(null);

        var inbox = await SendAsync(new GetMyNotificationsQuery());
        inbox.Items.Select(n => n.MessageKey).Should().BeEquivalentTo(
            "platformReportOpened", "platformReportCustomerWrote",
            "platformReportWithdrawn", "platformReportReopened");
        inbox.Items.Should().OnlyContain(n =>
            n.SubjectId == reportId && n.Link == "/agency-reports" && n.Args!["agency"] == "Test Agency");

        (await SendAsync(new GetMyUnreadNotificationCountQuery())).Should().Be(4);
        (await SendAsync(new MarkNotificationsReadCommand())).Should().Be(4);
        (await SendAsync(new GetMyUnreadNotificationCountQuery())).Should().Be(0);

        // The agency's inbox is a different table: none of this reaches it.
        BeTheAgency(booking);
        (await SendAsync(new GetMyNotificationsQuery())).Items
            .Should().NotContain(n => n.MessageKey.StartsWith("platformReport"));
    }

    [Test]
    public async Task TheCustomerIsMailedTheReplyAndTheVerdictAtTheAddressTheyGave()
    {
        var booking = await ConfirmedBookingAsync("rep41@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking, notifyEmail: " rep41-mail@example.com ");

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);

        (await SendAsync(new GetAgencyReportsQuery(AgencyReportStatus.Open)))
            .Items.Single().CustomerGetsEmail.Should().BeTrue();

        await SendAsync(new PostAgencyReportMessageCommand(reportId, "Do you have the booking confirmation?"));
        await SendAsync(new ResolveAgencyReportCommand(reportId, true, "The agency broke a confirmed booking."));

        var mails = RecordingEmailSender.SentTo("rep41-mail@example.com");
        mails.Should().HaveCount(2);
        mails[0].HtmlBody.Should().Contain("Do you have the booking confirmation?").And.Contain($"#{reportId}");
        mails[1].HtmlBody.Should().Contain("The agency broke a confirmed booking.");
        mails[0].Subject.Should().NotBe(mails[1].Subject);
    }

    [Test]
    public async Task NoAddressMeansNoMail()
    {
        var booking = await ConfirmedBookingAsync("rep42@local");
        await CancelAsAgencyAsync(booking);
        var reportId = await ReportAsCustomerAsync(booking);

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);

        (await SendAsync(new GetAgencyReportsQuery(AgencyReportStatus.Open)))
            .Items.Single().CustomerGetsEmail.Should().BeFalse();

        await SendAsync(new PostAgencyReportMessageCommand(reportId, "Anything to add?"));

        // Not even to the account's own address: the customer chose not to give one.
        RecordingEmailSender.SentTo("rep42@local").Should().BeEmpty();
    }

    [Test]
    public async Task AnInvalidAddressIsRefused()
    {
        var booking = await ConfirmedBookingAsync("rep43@local");
        await CancelAsAgencyAsync(booking);

        await FluentActions.Invoking(() => ReportAsCustomerAsync(booking, notifyEmail: "not-an-address"))
            .Should().ThrowAsync<ValidationException>();
    }

    // ----------------------------------------------------------------- setup ---

    private sealed record Booking(string CustomerId, string AgencyAdminId, int AgencyId, int ReservationId);

    /// <summary>A customer's request, still waiting on the agency.</summary>
    private static async Task<Booking> PendingBookingAsync(string userName)
    {
        var customerId = await RunAsUserAsync(userName, "Customer1234!", new[] { Roles.Customer });
        var agencyId = await AddTestAgencyAsync();

        var car = new Car { Matricule = "MK-REP", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") };
        await AddAsync(car);

        // The agency administrator is created here rather than per test: the two
        // sides take turns, and both accounts have to exist before either acts.
        var adminId = await RunAsUserAsync(
            $"admin-{userName}", "AgencyAdmin1234!", new[] { Roles.AgencyAdministrator });

        SetCurrentUser(customerId);
        SetCurrentAgency(null); // a customer has no tenant of their own

        var reservationId = await SendAsync(new CreateCustomerReservationCommand
        {
            CarId = car.Id, StartDate = Start, EndDate = End,
            FirstName = "Jane", LastName = "Doe", BirthDate = Dob
        });

        return new Booking(customerId, adminId, agencyId, reservationId);
    }

    /// <summary>The same request, now a promise the agency has made.</summary>
    private static async Task<Booking> ConfirmedBookingAsync(string userName)
    {
        var booking = await PendingBookingAsync(userName);

        BeTheAgency(booking);
        await SendAsync(new ConfirmReservationCommand(booking.ReservationId));

        return booking;
    }

    private static void BeTheAgency(Booking booking)
    {
        SetCurrentUser(booking.AgencyAdminId);
        SetCurrentAgency(booking.AgencyId);
    }

    private static void BeTheCustomer(Booking booking)
    {
        SetCurrentUser(booking.CustomerId);
        SetCurrentAgency(null);
    }

    private static async Task CancelAsAgencyAsync(Booking booking, string reason = "We are short of cars.")
    {
        BeTheAgency(booking);
        await SendAsync(new CancelReservationCommand(booking.ReservationId, reason));
    }

    private static async Task<int> ReportAsCustomerAsync(
        Booking booking, string message = "They let me down.", string? notifyEmail = null)
    {
        BeTheCustomer(booking);

        return await SendAsync(new CreateMyReportCommand
        {
            ReservationId = booking.ReservationId,
            Kind = AgencyReportKind.CancelledBooking,
            Message = message,
            NotifyEmail = notifyEmail
        });
    }
}
