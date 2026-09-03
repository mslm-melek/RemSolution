using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Marketplace.Commands.CreateCustomerReservationCommand;
using RemSolution.Application.Features.Marketplace.Commands.SubmitMyReservationRequirementCommand;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyReservationRequirementsQuery;
using RemSolution.Application.Features.Reservation.Commands.ConfirmReservationCommand;
using RemSolution.Application.Features.Reservation.Commands.ConvertReservationCommand;
using RemSolution.Application.Features.Reservation.Commands.CreateReservationCommand;
using RemSolution.Application.Features.Reservation.Commands.ReviewReservationRequirementCommand;
using RemSolution.Application.Features.Reservation.Commands.SetReservationRequirementsCommand;
using RemSolution.Application.Features.Reservation.Queries.GetReservationRequirementsQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Reservations.Commands;

using static Testing;

// What the agency asks for before the keys change hands: the agency declares it,
// the customer answers from their own screen, the agency looks at the answer, and
// nothing outstanding gets past the conversion without somebody saying so.
public class ReservationRequirementTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 7, 4, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Dob = new(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>A confirmed hold at a fresh agency, signed in as the agency admin.</summary>
    private static async Task<int> AConfirmedHoldAsync(string matricule)
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Reservations, Enabled = true });
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        var car = new Car { Matricule = matricule, Status = CarStatus.Active, DailyRate = Money.Of(30m, "TND") };
        await AddAsync(car);
        var client = new Client { FirstName = "Req", LastName = "Client" };
        await AddAsync(client);

        var id = await SendAsync(new CreateReservationCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End
        });

        await SendAsync(new ConfirmReservationCommand(id));

        return id;
    }

    private static SetReservationRequirementsCommand TwoAsks(int reservationId) => new()
    {
        Id = reservationId,
        Items = new List<ReservationRequirementItem>
        {
            new() { Kind = ReservationRequirementKind.Payment, Label = "Bank transfer", Amount = 90m, ExpectedMethod = PaymentMethod.Transfer },
            new() { Kind = ReservationRequirementKind.Document, Label = "Copy of the driving licence" },
        }
    };

    [Test]
    public async Task ShouldRecordWhatTheAgencyAsksFor()
    {
        var reservationId = await AConfirmedHoldAsync("REQ-1");

        await SendAsync(TwoAsks(reservationId));

        var asks = await SendAsync(new GetReservationRequirementsQuery(reservationId));

        asks.Should().HaveCount(2);
        asks[0].Label.Should().Be("Bank transfer");
        // Amounts are denominated in the agency's own currency, never the caller's.
        asks[0].Amount!.Amount.Should().Be(90m);
        asks[0].Amount!.Currency.Should().Be("TND");
        asks.Should().OnlyContain(a => a.Status == ReservationRequirementStatus.Requested);
    }

    /// <summary>
    /// Asking a customer for their papers before answering their request is
    /// asking them to work for an answer that may never come.
    /// </summary>
    [Test]
    public async Task ShouldRefuseToAskAnythingOfAHoldNobodyHasConfirmed()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Reservations, Enabled = true });

        var car = new Car { Matricule = "REQ-2", Status = CarStatus.Active, DailyRate = Money.Of(30m, "TND") };
        await AddAsync(car);
        var client = new Client { FirstName = "Req", LastName = "Client" };
        await AddAsync(client);

        var reservationId = await SendAsync(new CreateReservationCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End
        });

        await FluentActions.Invoking(() => SendAsync(TwoAsks(reservationId)))
            .Should().ThrowAsync<ValidationException>();
    }

    /// <summary>
    /// Re-sending the list replaces it — but an ask somebody already answered is
    /// waived rather than deleted, so their file and the fact it was once
    /// required both survive.
    /// </summary>
    [Test]
    public async Task ResendingTheListShouldDropUnansweredAsksAndWaiveAnsweredOnes()
    {
        var reservationId = await AConfirmedHoldAsync("REQ-3");

        await SendAsync(TwoAsks(reservationId));

        var asks = await SendAsync(new GetReservationRequirementsQuery(reservationId));
        var answered = asks.First(a => a.Kind == ReservationRequirementKind.Payment);

        await SendAsync(new ReviewReservationRequirementCommand
        {
            Id = answered.Id, Decision = ReservationRequirementDecision.Accept
        });

        // Neither ask is sent back this time.
        await SendAsync(new SetReservationRequirementsCommand
        {
            Id = reservationId,
            Items = new List<ReservationRequirementItem>()
        });

        var after = await SendAsync(new GetReservationRequirementsQuery(reservationId));

        after.Should().ContainSingle()
            .Which.Status.Should().Be(ReservationRequirementStatus.Waived);
    }

    [Test]
    public async Task RejectingShouldTellTheCustomerWhatToSendInstead()
    {
        var reservationId = await AConfirmedHoldAsync("REQ-4");
        await SendAsync(TwoAsks(reservationId));

        var ask = (await SendAsync(new GetReservationRequirementsQuery(reservationId)))
            .First(a => a.Kind == ReservationRequirementKind.Document);

        await FluentActions.Invoking(() => SendAsync(new ReviewReservationRequirementCommand
        {
            Id = ask.Id, Decision = ReservationRequirementDecision.Reject
        })).Should().ThrowAsync<ValidationException>();

        await SendAsync(new ReviewReservationRequirementCommand
        {
            Id = ask.Id,
            Decision = ReservationRequirementDecision.Reject,
            Note = "The scan is unreadable."
        });

        var after = await FindAsync<ReservationRequirement>(ask.Id);
        after!.Status.Should().Be(ReservationRequirementStatus.Rejected);
        after.ReviewNote.Should().Be("The scan is unreadable.");
    }

    /// <summary>
    /// The agent converting is often not the one who set the conditions, so an
    /// outstanding ask is a refusal rather than a warning — overridable in one
    /// field when the customer is standing at the counter with it.
    /// </summary>
    [Test]
    public async Task ConversionShouldStopWhileSomethingIsOutstanding()
    {
        var reservationId = await AConfirmedHoldAsync("REQ-5");
        await SendAsync(TwoAsks(reservationId));

        await FluentActions.Invoking(() => SendAsync(new ConvertReservationCommand { Id = reservationId }))
            .Should().ThrowAsync<ValidationException>();

        // Ticked off at the counter, one by one.
        foreach (var ask in await SendAsync(new GetReservationRequirementsQuery(reservationId)))
        {
            await SendAsync(new ReviewReservationRequirementCommand
            {
                Id = ask.Id, Decision = ReservationRequirementDecision.Accept
            });
        }

        var rentingId = await SendAsync(new ConvertReservationCommand { Id = reservationId });
        rentingId.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task ConversionShouldGoAheadWhenTheAgentSaysSo()
    {
        var reservationId = await AConfirmedHoldAsync("REQ-6");
        await SendAsync(TwoAsks(reservationId));

        var rentingId = await SendAsync(new ConvertReservationCommand
        {
            Id = reservationId, AcknowledgeUnmetRequirements = true
        });

        rentingId.Should().BeGreaterThan(0);
    }

    // ---------------------------------------------------------------------
    // The customer's half
    // ---------------------------------------------------------------------

    [Test]
    public async Task ACustomerShouldSeeAndAnswerTheirOwnChecklist()
    {
        var customerId = await RunAsUserAsync("req-cust@local", "Customer1234!", new[] { Roles.Customer });
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Reservations, Enabled = true });

        var car = new Car { Matricule = "REQ-7", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") };
        await AddAsync(car);
        SetCurrentAgency(null); // a customer has no tenant of their own

        var reservationId = await SendAsync(new CreateCustomerReservationCommand
        {
            CarId = car.Id, StartDate = Start, EndDate = End,
            FirstName = "Jane", LastName = "Doe", BirthDate = Dob
        });

        // The agency confirms, then says what it needs.
        await RunAsAgencyAdministratorAsync();
        SetCurrentAgency(await CurrentAgencyIdOfAsync(reservationId));
        await SendAsync(new ConfirmReservationCommand(reservationId));
        await SendAsync(new SetReservationRequirementsCommand
        {
            Id = reservationId,
            Items = new List<ReservationRequirementItem>
            {
                new() { Kind = ReservationRequirementKind.Conditions, Label = "Accept the rental terms" },
            }
        });

        // Back to the customer — the account already exists.
        SetCurrentUser(customerId);
        SetCurrentAgency(null);

        var mine = await SendAsync(new GetMyReservationRequirementsQuery(reservationId));
        mine.Should().ContainSingle();

        await SendAsync(new SubmitMyReservationRequirementCommand
        {
            Id = mine[0].Id, Note = "Accepted"
        });

        var after = await SendAsync(new GetMyReservationRequirementsQuery(reservationId));
        after[0].Status.Should().Be(ReservationRequirementStatus.Submitted);
        after[0].SubmittedNote.Should().Be("Accepted");
        after[0].SubmittedAt.Should().NotBeNull();
    }

    [Test]
    public async Task ACustomerShouldNotSeeSomebodyElsesChecklist()
    {
        var reservationId = await AConfirmedHoldAsync("REQ-8");
        await SendAsync(TwoAsks(reservationId));

        await RunAsUserAsync("nosy@local", "Customer1234!", new[] { Roles.Customer });
        SetCurrentAgency(null);

        // Not a refusal: someone else's reservation is indistinguishable from one
        // that does not exist.
        (await SendAsync(new GetMyReservationRequirementsQuery(reservationId))).Should().BeEmpty();

        await FluentActions.Invoking(() => SendAsync(new SubmitMyReservationRequirementCommand
        {
            Id = 1, Note = "Let me in"
        })).Should().ThrowAsync<Exception>();
    }

    private static async Task<int> CurrentAgencyIdOfAsync(int reservationId)
    {
        var reservation = await FindIgnoringFiltersAsync<Reservation>(r => r.Id == reservationId);
        return reservation!.AgencyId;
    }
}
