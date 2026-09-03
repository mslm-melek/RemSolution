using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Reservation.Commands.CancelReservationCommand;
using RemSolution.Application.Features.Reservation.Commands.ConfirmReservationCommand;
using RemSolution.Application.Features.Reservation.Commands.ConvertReservationCommand;
using RemSolution.Application.Features.Reservation.Commands.CreateReservationCommand;
using RemSolution.Application.Features.Renting.Commands.CreateRentingCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Reservations.Commands;

using static Testing;

public class ConfirmReservationTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 6, 4, 0, 0, 0, DateTimeKind.Utc);

    // Confirm only approves the hold; conversion is a separate step that creates
    // the renting (Phase 3 lifecycle).
    [Test]
    public async Task ConfirmApprovesHoldWithoutCreatingRenting()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Reservations, Enabled = true });

        var car = new Car { Matricule = "RES-1", Status = CarStatus.Active, DailyRate = Money.Of(30m, "TND") };
        await AddAsync(car);
        var client = new Client { FirstName = "Test", LastName = "Client" };
        await AddAsync(client);

        var reservationId = await SendAsync(new CreateReservationCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End
        });

        var reservation = await FindAsync<Reservation>(reservationId);
        reservation!.Status.Should().Be(ReservationStatus.PendingConfirmation);
        reservation.ExpiresAt.Should().NotBeNull();
        reservation.Price!.Amount.Should().Be(90m); // 3 days × 30

        await SendAsync(new ConfirmReservationCommand(reservationId));

        var confirmed = await FindAsync<Reservation>(reservationId);
        confirmed!.Status.Should().Be(ReservationStatus.Confirmed);
        confirmed.RentingId.Should().BeNull(); // no renting yet
    }

    [Test]
    public async Task ConvertCreatesRentingFromConfirmedHold()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Reservations, Enabled = true });
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        var car = new Car { Matricule = "RES-C", Status = CarStatus.Active, DailyRate = Money.Of(30m, "TND") };
        await AddAsync(car);
        var client = new Client { FirstName = "Test", LastName = "Client" };
        await AddAsync(client);

        var reservationId = await SendAsync(new CreateReservationCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End
        });
        await SendAsync(new ConfirmReservationCommand(reservationId));

        var rentingId = await SendAsync(new ConvertReservationCommand { Id = reservationId });

        var renting = await FindAsync<Renting>(rentingId);
        renting.Should().NotBeNull();
        renting!.AgencyId.Should().Be(agencyId);
        renting.Price!.Amount.Should().Be(90m); // kept from the hold, not re-quoted

        var converted = await FindAsync<Reservation>(reservationId);
        converted!.Status.Should().Be(ReservationStatus.Converted);
        converted.RentingId.Should().Be(rentingId);
    }

    [Test]
    public async Task ConvertBeforeConfirmIsRejected()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Reservations, Enabled = true });
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        var car = new Car { Matricule = "RES-NC", Status = CarStatus.Active, DailyRate = Money.Of(30m, "TND") };
        await AddAsync(car);
        var client = new Client { FirstName = "Test", LastName = "Client" };
        await AddAsync(client);

        var reservationId = await SendAsync(new CreateReservationCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End
        });

        // Still PendingConfirmation — cannot convert.
        await FluentActions.Invoking(() => SendAsync(new ConvertReservationCommand { Id = reservationId }))
            .Should().ThrowAsync<Exception>();
    }

    [Test]
    public async Task ShouldBlockRentingThatOverlapsAConfirmedReservation()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Reservations, Enabled = true });
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        var car = new Car { Matricule = "RES-2", Status = CarStatus.Active, DailyRate = Money.Of(30m, "TND") };
        await AddAsync(car);
        var client = new Client { FirstName = "Test", LastName = "Client" };
        await AddAsync(client);

        var held = await SendAsync(new CreateReservationCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End
        });
        await SendAsync(new ConfirmReservationCommand(held));

        await FluentActions.Invoking(() => SendAsync(new CreateRentingCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start.AddDays(1), EndDate = End.AddDays(1)
        })).Should().ThrowAsync<BookingConflictException>();
    }

    // A request is a question, not a claim: it must not take the car off the
    // counter while the agency is still thinking about it.
    [Test]
    public async Task APendingRequestShouldNotBlockAnything()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Reservations, Enabled = true });
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        var car = new Car { Matricule = "RES-3", Status = CarStatus.Active, DailyRate = Money.Of(30m, "TND") };
        await AddAsync(car);
        var client = new Client { FirstName = "Test", LastName = "Client" };
        await AddAsync(client);

        await SendAsync(new CreateReservationCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End
        });

        // A second customer may ask for the same car and the same days.
        var second = await SendAsync(new CreateReservationCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End
        });
        second.Should().BeGreaterThan(0);

        // And a walk-in hire still goes through.
        var rentingId = await SendAsync(new CreateRentingCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End
        });
        rentingId.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task ConfirmShouldRefuseWhileAnotherBookingHoldsThePeriod()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Reservations, Enabled = true });

        var car = new Car { Matricule = "RES-4", Status = CarStatus.Active, DailyRate = Money.Of(30m, "TND") };
        await AddAsync(car);
        var client = new Client { FirstName = "Test", LastName = "Client" };
        await AddAsync(client);

        var first = await SendAsync(new CreateReservationCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End
        });
        var second = await SendAsync(new CreateReservationCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start.AddDays(1), EndDate = End.AddDays(1)
        });

        await SendAsync(new ConfirmReservationCommand(first));

        var conflict = await FluentActions.Invoking(() => SendAsync(new ConfirmReservationCommand(second)))
            .Should().ThrowAsync<BookingConflictException>();
        // The agency is told which hold to clear, not just that it cannot proceed.
        conflict.Which.Kind.Should().Be(BookingConflictKind.Reservation);
        conflict.Which.ConflictingId.Should().Be(first);

        (await FindAsync<Reservation>(second))!.Status
            .Should().Be(ReservationStatus.PendingConfirmation);
    }

    [Test]
    public async Task ConfirmShouldSucceedOnceTheOtherHoldIsCleared()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Reservations, Enabled = true });

        var car = new Car { Matricule = "RES-5", Status = CarStatus.Active, DailyRate = Money.Of(30m, "TND") };
        await AddAsync(car);
        var client = new Client { FirstName = "Test", LastName = "Client" };
        await AddAsync(client);

        var first = await SendAsync(new CreateReservationCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End
        });
        var second = await SendAsync(new CreateReservationCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End
        });

        await SendAsync(new ConfirmReservationCommand(first));
        await SendAsync(new CancelReservationCommand(first, "Client changed their mind"));

        await SendAsync(new ConfirmReservationCommand(second));

        (await FindAsync<Reservation>(second))!.Status
            .Should().Be(ReservationStatus.Confirmed);
    }
}
