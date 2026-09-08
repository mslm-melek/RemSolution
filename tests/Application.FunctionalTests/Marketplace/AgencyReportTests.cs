using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Agency.Queries.GetMyAgencyReliabilityQuery;
using RemSolution.Application.Features.AgencyReport.Commands.ResolveAgencyReportCommand;
using RemSolution.Application.Features.AgencyReport.Queries.GetAgencyReportsQuery;
using RemSolution.Application.Features.AgencyReport.Queries.GetMyAgencyReportsQuery;
using RemSolution.Application.Features.Marketplace.Commands.CancelMyReservationCommand;
using RemSolution.Application.Features.Marketplace.Commands.CreateCustomerReservationCommand;
using RemSolution.Application.Features.Marketplace.Commands.CreateMyReportCommand;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMarketplaceAgencyQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyReportsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyReservationsQuery;
using RemSolution.Application.Features.Reservation.Commands.CancelReservationCommand;
using RemSolution.Application.Features.Reservation.Commands.ConfirmReservationCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
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
        Booking booking, string message = "They let me down.")
    {
        BeTheCustomer(booking);

        return await SendAsync(new CreateMyReportCommand
        {
            ReservationId = booking.ReservationId,
            Kind = AgencyReportKind.CancelledBooking,
            Message = message
        });
    }
}
