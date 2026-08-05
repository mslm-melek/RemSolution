using RemSolution.Application.Features.Reservation.Queries.GetReservationsWithPaginationQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.FunctionalTests.Reservations.Queries;

using static Testing;

// The home screen's "today" queue links into this list, so the window and the
// still-in-play filter have to select exactly the rows that queue showed — see
// FromDate/ToDate and ActiveOnly.
public class GetReservationsWithPaginationQueryTests : BaseTestFixture
{
    private static readonly DateTime Today = new(2030, 5, 10, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DayStart = new(2030, 5, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime NextDayStart = new(2030, 5, 11, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ShouldFilterByTheDayTheHoldStartsOn()
    {
        await SetUpAgencyAsync();

        await SeedReservationAsync("STARTS-TODAY", Today);
        // The evening before and the following morning: both are outside the
        // half-open day, which is what makes the queue's figure a day's work.
        await SeedReservationAsync("STARTS-YESTERDAY", DayStart.AddHours(-2));
        await SeedReservationAsync("STARTS-TOMORROW", NextDayStart.AddHours(8));

        var today = await SendAsync(new GetReservationsWithPaginationQuery
        {
            FromDate = DayStart, ToDate = NextDayStart
        });

        today.TotalCount.Should().Be(1);
        today.Items.First().CarMatricule.Should().Be("STARTS-TODAY");
    }

    [Test]
    public async Task ShouldKeepOnlyHoldsStillInPlayWhenAsked()
    {
        await SetUpAgencyAsync();

        await SeedReservationAsync("AWAITING", Today);
        await SeedReservationAsync("APPROVED", Today, ReservationStatus.Confirmed);
        await SeedReservationAsync("SETTLED", Today, ReservationStatus.Paid);
        await SeedReservationAsync("BECAME-A-HIRE", Today, ReservationStatus.Converted);
        await SeedReservationAsync("DECLINED", Today, ReservationStatus.Rejected);
        await SeedReservationAsync("CALLED-OFF", Today, ReservationStatus.Cancelled);
        await SeedReservationAsync("RAN-OUT", Today, ReservationStatus.Expired);

        (await SendAsync(new GetReservationsWithPaginationQuery())).TotalCount.Should().Be(7);

        var active = await SendAsync(new GetReservationsWithPaginationQuery { ActiveOnly = true });

        active.TotalCount.Should().Be(3);
        active.Items.Select(r => r.CarMatricule)
              .Should().BeEquivalentTo(new[] { "AWAITING", "APPROVED", "SETTLED" });
    }

    [Test]
    public async Task ShouldCombineTheWindowWithASingleStatus()
    {
        await SetUpAgencyAsync();

        await SeedReservationAsync("TODAY-AWAITING", Today);
        await SeedReservationAsync("TODAY-APPROVED", Today, ReservationStatus.Confirmed);
        await SeedReservationAsync("TOMORROW-AWAITING", NextDayStart.AddHours(8));

        var pendingToday = await SendAsync(new GetReservationsWithPaginationQuery
        {
            FromDate = DayStart,
            ToDate = NextDayStart,
            Status = ReservationStatus.PendingConfirmation
        });

        pendingToday.TotalCount.Should().Be(1);
        pendingToday.Items.First().CarMatricule.Should().Be("TODAY-AWAITING");
    }

    private static async Task SetUpAgencyAsync()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Reservations, Enabled = true });
    }

    // Seeded straight into the context rather than through CreateReservationCommand:
    // the point is the stored start date and status, not the booking rules. The
    // status is still reached by walking the aggregate's own transitions — it has no
    // setter, deliberately (see Reservation).
    private static async Task SeedReservationAsync(
        string matricule,
        DateTime start,
        ReservationStatus status = ReservationStatus.PendingConfirmation)
    {
        var car = new Car { Matricule = matricule, Status = CarStatus.Active };
        await AddAsync(car);

        var end = start.AddDays(3);
        var reservation = Reservation.Create(car.Id, start, end, null, expiresAt: start.AddDays(-1));

        switch (status)
        {
            case ReservationStatus.PendingConfirmation:
                break;
            case ReservationStatus.Confirmed:
                reservation.Confirm();
                break;
            case ReservationStatus.Paid:
                reservation.Confirm();
                reservation.MarkPaid();
                break;
            case ReservationStatus.Converted:
                reservation.Confirm();
                reservation.Convert(new Renting { CarId = car.Id, StartDate = start, EndDate = end });
                break;
            case ReservationStatus.Rejected:
                reservation.Reject("That car is spoken for.");
                break;
            case ReservationStatus.Cancelled:
                reservation.Cancel("The client changed their plans.");
                break;
            case ReservationStatus.Expired:
                reservation.Expire();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        await AddAsync(reservation);
    }
}
