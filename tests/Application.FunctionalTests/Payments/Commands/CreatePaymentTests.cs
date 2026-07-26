using RemSolution.Application.Features.Payment.Commands.CreatePaymentCommand;
using RemSolution.Application.Features.Payment.Queries.GetClientBalanceQuery;
using RemSolution.Application.Features.Reservation.Commands.ConfirmReservationCommand;
using RemSolution.Application.Features.Reservation.Commands.CreateReservationCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Payments.Commands;

using static Testing;

public class CreatePaymentTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 8, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 8, 4, 0, 0, 0, DateTimeKind.Utc);

    // Confirmed hold of 3 × 30 = 90 TND, ready to receive payment.
    private async Task<(int reservationId, int clientId)> ConfirmedHoldAsync(string matricule)
    {
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Reservations, Enabled = true });
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Payments, Enabled = true });

        var car = new Car { Matricule = matricule, Status = CarStatus.Active, DailyRate = Money.Of(30m, "TND") };
        await AddAsync(car);
        var client = new Client { FirstName = "Pay", LastName = "Client" };
        await AddAsync(client);

        var reservationId = await SendAsync(new CreateReservationCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End
        });
        await SendAsync(new ConfirmReservationCommand(reservationId));

        return (reservationId, client.Id);
    }

    [Test]
    public async Task FullPaymentFlipsReservationToPaid()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (reservationId, _) = await ConfirmedHoldAsync("PAY-1");

        await SendAsync(new CreatePaymentCommand { ReservationId = reservationId, Amount = 90m });

        var reservation = await FindAsync<Reservation>(reservationId);
        reservation!.Status.Should().Be(ReservationStatus.Paid);
        reservation.PayedPrice!.Amount.Should().Be(90m);
    }

    [Test]
    public async Task PartialPaymentLeavesReservationConfirmed()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (reservationId, _) = await ConfirmedHoldAsync("PAY-2");

        await SendAsync(new CreatePaymentCommand { ReservationId = reservationId, Amount = 40m });

        var reservation = await FindAsync<Reservation>(reservationId);
        reservation!.Status.Should().Be(ReservationStatus.Confirmed);
        reservation.PayedPrice!.Amount.Should().Be(40m);
    }

    [Test]
    public async Task OverpaymentIsRejected()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (reservationId, _) = await ConfirmedHoldAsync("PAY-3");

        await SendAsync(new CreatePaymentCommand { ReservationId = reservationId, Amount = 50m });

        await FluentActions.Invoking(() => SendAsync(new CreatePaymentCommand
        {
            ReservationId = reservationId, Amount = 50m // 50 + 50 > 90
        })).Should().ThrowAsync<Exception>();
    }

    [Test]
    public async Task ClientBalanceReflectsChargesMinusPayments()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (reservationId, clientId) = await ConfirmedHoldAsync("PAY-4");

        await SendAsync(new CreatePaymentCommand { ReservationId = reservationId, Amount = 40m });

        var balance = await SendAsync(new GetClientBalanceQuery(clientId));

        balance!.TotalCharged!.Amount.Should().Be(90m);
        balance.TotalPaid!.Amount.Should().Be(40m);
        balance.Balance!.Amount.Should().Be(50m);
    }
}
