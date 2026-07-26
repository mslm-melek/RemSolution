using RemSolution.Application.Features.Reservation.Commands.ConfirmReservationCommand;
using RemSolution.Application.Features.Reservation.Commands.CreateReservationCommand;
using RemSolution.Application.Features.Reservation.Commands.RejectReservationCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Reservations.Commands;

using static Testing;

public class RejectReservationTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 7, 4, 0, 0, 0, DateTimeKind.Utc);

    private async Task<int> CreatePendingHoldAsync(string matricule)
    {
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Reservations, Enabled = true });
        var car = new Car { Matricule = matricule, Status = CarStatus.Active, DailyRate = Money.Of(30m, "TND") };
        await AddAsync(car);
        var client = new Client { FirstName = "Test", LastName = "Client" };
        await AddAsync(client);

        return await SendAsync(new CreateReservationCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End
        });
    }

    [Test]
    public async Task RejectStoresReasonAndBlocksFurtherTransitions()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var reservationId = await CreatePendingHoldAsync("REJ-1");

        await SendAsync(new RejectReservationCommand(reservationId, "No cars available that week."));

        var rejected = await FindAsync<Reservation>(reservationId);
        rejected!.Status.Should().Be(ReservationStatus.Rejected);
        rejected.RejectedReason.Should().Be("No cars available that week.");

        // A rejected hold can no longer be confirmed.
        await FluentActions.Invoking(() => SendAsync(new ConfirmReservationCommand(reservationId)))
            .Should().ThrowAsync<Exception>();
    }

    [Test]
    public async Task RejectRequiresAReason()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var reservationId = await CreatePendingHoldAsync("REJ-2");

        await FluentActions.Invoking(() => SendAsync(new RejectReservationCommand(reservationId, "")))
            .Should().ThrowAsync<Exception>();
    }
}
