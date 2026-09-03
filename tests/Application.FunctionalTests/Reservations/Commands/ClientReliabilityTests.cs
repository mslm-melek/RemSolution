using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Agency.Commands.UpdateMyAgencyCommand;
using RemSolution.Application.Features.Client.Queries.GetClientReliabilityQuery;
using RemSolution.Application.Features.Marketplace.Commands.CancelMyReservationCommand;
using RemSolution.Application.Features.Marketplace.Commands.CreateCustomerReservationCommand;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyReservationsQuery;
using RemSolution.Application.Features.Reservation.Commands.CancelReservationCommand;
using RemSolution.Application.Features.Reservation.Commands.ConfirmReservationCommand;
using RemSolution.Application.Features.Reservation.Commands.CreateReservationCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Reservations.Commands;

using static Testing;

// What a customer's record with an agency says about them, and what calling a
// booking off costs. The two are the same feature: only a cancellation the
// CUSTOMER made counts, and only a late one costs.
public class ClientReliabilityTests : BaseTestFixture
{
    // Far enough out that a cancellation is free under the default policy, and
    // still movable to the paying band by the tests that need it.
    private static readonly DateTime Start = new(2030, 8, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 8, 4, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Dob = new(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    private static async Task<(int AgencyId, int ClientId, int CarId)> AnAgencyWithAClientAsync(
        string matricule)
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Reservations, Enabled = true });
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        var car = new Car { Matricule = matricule, Status = CarStatus.Active, DailyRate = Money.Of(30m, "TND") };
        await AddAsync(car);

        var client = new Client { FirstName = "Rely", LastName = "Client" };
        await AddAsync(client);

        return (agencyId, client.Id, car.Id);
    }

    [Test]
    public async Task ACleanRecordShouldScoreFull()
    {
        var (_, clientId, _) = await AnAgencyWithAClientAsync("REL-1");

        var record = await SendAsync(new GetClientReliabilityQuery(clientId));

        record.Score.Should().Be(100);
        record.Bookings.Should().Be(0);
        record.Cancellations.Should().Be(0);
    }

    /// <summary>
    /// The agency calling its own booking off must not cost the customer points:
    /// charging someone for a decision they did not take is the wrong way round.
    /// </summary>
    [Test]
    public async Task AnAgencyCancellationShouldNotCountAgainstTheCustomer()
    {
        var (_, clientId, carId) = await AnAgencyWithAClientAsync("REL-2");

        var reservationId = await SendAsync(new CreateReservationCommand
        {
            CarId = carId, ClientId = clientId, StartDate = Start, EndDate = End
        });

        await SendAsync(new CancelReservationCommand(reservationId, "Car went to the garage"));

        var record = await SendAsync(new GetClientReliabilityQuery(clientId));

        record.Cancellations.Should().Be(0);
        record.Score.Should().Be(100);

        var reservation = await FindAsync<Reservation>(reservationId);
        reservation!.CancelledByCustomer.Should().BeFalse();
        reservation.CancelledAt.Should().NotBeNull();
        reservation.CancellationFee.Should().BeNull();
    }

    [Test]
    public async Task ACustomerCancellationShouldCostPoints()
    {
        var customerId = await RunAsUserAsync("rely-cust@local", "Customer1234!", new[] { Roles.Customer });
        var adminId = await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Reservations, Enabled = true });

        var car = new Car { Matricule = "REL-3", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") };
        await AddAsync(car);

        SetCurrentUser(customerId);
        SetCurrentAgency(null); // a customer has no tenant of their own

        var reservationId = await SendAsync(new CreateCustomerReservationCommand
        {
            CarId = car.Id, StartDate = Start, EndDate = End,
            FirstName = "Jane", LastName = "Doe", BirthDate = Dob
        });

        await SendAsync(new CancelMyReservationCommand(reservationId, "Plans changed"));

        var reservation = await FindIgnoringFiltersAsync<Reservation>(r => r.Id == reservationId);
        reservation!.CancelledByCustomer.Should().BeTrue();
        reservation.CancelledReason.Should().Contain("Plans changed");
        // Far out and no policy set, so it costs nothing.
        reservation.CancellationFee.Should().BeNull();

        SetCurrentUser(adminId);
        SetCurrentAgency(agencyId);

        var record = await SendAsync(new GetClientReliabilityQuery(reservation.ClientId!.Value));

        record.Cancellations.Should().Be(1);
        record.LateCancellations.Should().Be(0);
        record.Score.Should().Be(85); // 100 âˆ’ 15
    }

    /// <summary>
    /// A hold called off inside the agency's free window carries the fee, frozen
    /// on the reservation â€” the policy can change later, what was charged cannot.
    /// </summary>
    [Test]
    public async Task ALateCustomerCancellationShouldCarryTheFee()
    {
        var customerId = await RunAsUserAsync("rely-late@local", "Customer1234!", new[] { Roles.Customer });
        var adminId = await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Reservations, Enabled = true });

        // 30 TND flat, free up to 30 days ahead â€” which the seeded start date is
        // comfortably inside, so a cancellation now is a late one.
        var country = new Country { Name = "Feeland" };
        await AddAsync(country);

        await SendAsync(new UpdateMyAgencyCommand
        {
            Name = "Test Agency",
            CountryId = country.Id,
            CancellationWindowHours = 24,
            ReservationExpiryHours = 48,
            CancellationFeeMode = CancellationFeeMode.FixedAmount,
            CancellationFeeValue = 30m,
            CancellationFreeHours = 24 * 30,
        });

        var car = new Car { Matricule = "REL-4", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") };
        await AddAsync(car);

        SetCurrentUser(customerId);
        SetCurrentAgency(null);

        // Five days out: past the 24h cutoff, inside the 30-day free window, so
        // this is the band that charges.
        var soon = DateTime.UtcNow.Date.AddDays(5);

        var reservationId = await SendAsync(new CreateCustomerReservationCommand
        {
            CarId = car.Id, StartDate = soon, EndDate = soon.AddDays(3),
            FirstName = "Late", LastName = "Doe", BirthDate = Dob
        });

        // The customer is told the cost before deciding, by the same policy the
        // command applies.
        var mine = await SendAsync(new GetMyReservationsQuery());
        var row = mine.Single(r => r.Id == reservationId);
        row.CanCancel.Should().BeTrue();
        row.CancellationFeeIfCancelledNow!.Amount.Should().Be(30m);

        await SendAsync(new CancelMyReservationCommand(reservationId, "Cheaper elsewhere"));

        var reservation = await FindIgnoringFiltersAsync<Reservation>(r => r.Id == reservationId);
        reservation!.CancellationFee!.Amount.Should().Be(30m);
        reservation.CancellationFee.Currency.Should().Be("TND");

        SetCurrentUser(adminId);
        SetCurrentAgency(agencyId);

        var record = await SendAsync(new GetClientReliabilityQuery(reservation.ClientId!.Value));

        record.Cancellations.Should().Be(1);
        record.LateCancellations.Should().Be(1);
        record.Score.Should().Be(70); // 100 âˆ’ 15 âˆ’ 15
    }

    /// <summary>
    /// The hard cutoff belongs to both sides: once a booking is within it, nobody
    /// can call it off â€” the fee band stops there.
    /// </summary>
    [Test]
    public async Task ACustomerCannotCancelInsideTheCutoff()
    {
        var customerId = await RunAsUserAsync("rely-cutoff@local", "Customer1234!", new[] { Roles.Customer });
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Reservations, Enabled = true });

        var car = new Car { Matricule = "REL-5", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") };
        await AddAsync(car);

        SetCurrentUser(customerId);
        SetCurrentAgency(null);

        // Inside the agency's 24h cutoff, with hours to spare either side: an
        // hour would make the test depend on the suite finishing promptly.
        var soon = DateTime.UtcNow.AddHours(6);

        var reservationId = await SendAsync(new CreateCustomerReservationCommand
        {
            CarId = car.Id, StartDate = soon, EndDate = soon.AddDays(2),
            FirstName = "Soon", LastName = "Doe", BirthDate = Dob
        });

        var mine = await SendAsync(new GetMyReservationsQuery());
        mine.Single(r => r.Id == reservationId).CanCancel.Should().BeFalse();

        await FluentActions.Invoking(() => SendAsync(new CancelMyReservationCommand(reservationId)))
            .Should().ThrowAsync<ValidationException>();
    }

    /// <summary>
    /// A confirmed hold is exactly where a cancellation costs something, so the
    /// customer has to be able to call one off â€” they could not before.
    /// </summary>
    [Test]
    public async Task ACustomerCanCancelAConfirmedHold()
    {
        var customerId = await RunAsUserAsync("rely-conf@local", "Customer1234!", new[] { Roles.Customer });
        var adminId = await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Reservations, Enabled = true });

        var car = new Car { Matricule = "REL-6", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") };
        await AddAsync(car);

        SetCurrentUser(customerId);
        SetCurrentAgency(null);

        var reservationId = await SendAsync(new CreateCustomerReservationCommand
        {
            CarId = car.Id, StartDate = Start, EndDate = End,
            FirstName = "Conf", LastName = "Doe", BirthDate = Dob
        });

        SetCurrentUser(adminId);
        SetCurrentAgency(agencyId);
        await SendAsync(new ConfirmReservationCommand(reservationId));

        SetCurrentUser(customerId);
        SetCurrentAgency(null);

        await SendAsync(new CancelMyReservationCommand(reservationId, "Something came up"));

        var reservation = await FindIgnoringFiltersAsync<Reservation>(r => r.Id == reservationId);
        reservation!.Status.Should().Be(ReservationStatus.Cancelled);
        reservation.CancelledByCustomer.Should().BeTrue();
    }
}

