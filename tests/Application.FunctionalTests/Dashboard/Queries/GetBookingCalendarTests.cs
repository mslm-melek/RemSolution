using RemSolution.Application.Features.Dashboard.DTOs;
using RemSolution.Application.Features.Dashboard.Queries.GetBookingCalendarQuery;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Dashboard.Queries;

using static Testing;

public class GetBookingCalendarTests : BaseTestFixture
{
    // A fixed month, so what lands in the window never depends on the day the
    // suite runs. The overdue test below is the one exception: "late" is measured
    // against the real clock, so it anchors itself on today.
    private static readonly DateTime MonthStart = new(2030, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime MonthEnd = new(2030, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime InMonth = new(2030, 4, 10, 9, 0, 0, DateTimeKind.Utc);

    private async Task<Car> AddCarAsync(string matricule)
    {
        var car = new Car { Matricule = matricule, Status = CarStatus.Active };
        await AddAsync(car);
        return car;
    }

    private async Task<Client> AddClientAsync(string firstName)
    {
        var client = new Client { FirstName = firstName, LastName = "Calendar" };
        await AddAsync(client);
        return client;
    }

    [Test]
    public async Task SplitsAHireIntoItsPickupAndItsReturn()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = await AddCarAsync("CAL-1");
        var client = await AddClientAsync("Split");

        var renting = new Renting
        {
            CarId = car.Id,
            ClientId = client.Id,
            StartDate = InMonth,
            EndDate = InMonth.AddDays(3),
            RentingState = RentingState.NotYet,
            Price = Money.Of(150m, "TND")
        };
        await AddAsync(renting);

        var result = await SendAsync(new GetBookingCalendarQuery(MonthStart, MonthEnd));

        result.From.Should().Be(MonthStart);
        result.To.Should().Be(MonthEnd);
        result.IsTruncated.Should().BeFalse();

        result.Events.Should().HaveCount(2, "the day it goes out and the day it comes back");

        var pickup = result.Events.Single(e => e.Kind == BookingCalendarEventKind.Pickup);
        pickup.On.Should().Be(InMonth);
        pickup.RentingId.Should().Be(renting.Id);
        pickup.ReservationId.Should().BeNull();
        pickup.CarMatricule.Should().Be("CAL-1");
        pickup.ClientName.Should().Be("Split Calendar");
        pickup.RentingState.Should().Be(RentingState.NotYet);
        pickup.IsLate.Should().BeFalse();

        var back = result.Events.Single(e => e.Kind == BookingCalendarEventKind.Return);
        back.On.Should().Be(InMonth.AddDays(3));
        back.RentingId.Should().Be(renting.Id);
    }

    [Test]
    public async Task OnlyTheDateInsideTheWindowMakesAnEntry()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = await AddCarAsync("CAL-2");
        var client = await AddClientAsync("Straddle");

        // Picked up the month before and due back inside the window: the return is
        // this month's work, the pickup was last month's.
        await AddAsync(new Renting
        {
            CarId = car.Id,
            ClientId = client.Id,
            StartDate = MonthStart.AddDays(-5),
            EndDate = InMonth,
            RentingState = RentingState.InProgress,
            Price = Money.Of(300m, "TND")
        });

        var result = await SendAsync(new GetBookingCalendarQuery(MonthStart, MonthEnd));

        result.Events.Should().ContainSingle()
            .Which.Kind.Should().Be(BookingCalendarEventKind.Return);
    }

    [Test]
    public async Task LeavesOutCancelledHiresAndHoldsThatWillNotHappen()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = await AddCarAsync("CAL-3");
        var client = await AddClientAsync("Dead");

        await AddAsync(new Renting
        {
            CarId = car.Id,
            ClientId = client.Id,
            StartDate = InMonth,
            EndDate = InMonth.AddDays(2),
            RentingState = RentingState.Cancelled,
            Price = Money.Of(90m, "TND")
        });

        var rejected = Reservation.Create(
            car.Id, InMonth, InMonth.AddDays(2), Money.Of(80m, "TND"), InMonth.AddHours(24), client.Id);
        rejected.Reject("Car needed elsewhere.");
        await AddAsync(rejected);

        var expired = Reservation.Create(
            car.Id, InMonth, InMonth.AddDays(2), Money.Of(80m, "TND"), InMonth.AddHours(24), client.Id);
        expired.Expire();
        await AddAsync(expired);

        var result = await SendAsync(new GetBookingCalendarQuery(MonthStart, MonthEnd));

        result.Events.Should().BeEmpty();
    }

    [Test]
    public async Task ShowsAHoldThatIsStillWaitingOnTheAgency()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = await AddCarAsync("CAL-4");
        var client = await AddClientAsync("Hold");

        var hold = Reservation.Create(
            car.Id, InMonth, InMonth.AddDays(2), Money.Of(80m, "TND"), InMonth.AddHours(24), client.Id);
        await AddAsync(hold);

        var result = await SendAsync(new GetBookingCalendarQuery(MonthStart, MonthEnd));

        var entry = result.Events.Should().ContainSingle().Subject;
        entry.Kind.Should().Be(BookingCalendarEventKind.ReservationStart);
        entry.ReservationId.Should().Be(hold.Id);
        entry.RentingId.Should().BeNull();
        entry.ReservationStatus.Should().Be(ReservationStatus.PendingConfirmation);
        entry.On.Should().Be(InMonth);
        // A hold contributes its start only: the day the car would go out. Its end
        // is not a return the desk has to handle until it becomes a hire.
        result.Events.Should().NotContain(e => e.Kind == BookingCalendarEventKind.Return);
    }

    [Test]
    public async Task FlagsAReturnThatIsAlreadyOverdue()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = await AddCarAsync("CAL-5");
        var client = await AddClientAsync("Late");

        // Anchored on the real clock: overdue means "due before now", the same rule
        // the client list's overdue count applies.
        var today = DateTime.UtcNow.Date;

        await AddAsync(new Renting
        {
            CarId = car.Id,
            ClientId = client.Id,
            StartDate = today.AddDays(-5),
            EndDate = today.AddDays(-1),
            RentingState = RentingState.InProgress,
            Price = Money.Of(120m, "TND")
        });

        // A finished hire that also came back before now is not late: it is back.
        await AddAsync(new Renting
        {
            CarId = car.Id,
            ClientId = client.Id,
            StartDate = today.AddDays(-5),
            EndDate = today.AddDays(-2),
            RentingState = RentingState.Done,
            Price = Money.Of(100m, "TND")
        });

        var result = await SendAsync(new GetBookingCalendarQuery(today.AddDays(-7), today.AddDays(1)));

        var returns = result.Events.Where(e => e.Kind == BookingCalendarEventKind.Return).ToList();
        returns.Should().HaveCount(2);
        returns.Should().ContainSingle(e => e.IsLate)
            .Which.RentingState.Should().Be(RentingState.InProgress);
    }

    [Test]
    public async Task ClampsAWindowLongerThanACalendarScreen()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        // A year, asked for through a screen that draws weeks.
        var result = await SendAsync(new GetBookingCalendarQuery(MonthStart, MonthStart.AddYears(1)));

        result.From.Should().Be(MonthStart);
        result.To.Should().BeBefore(MonthStart.AddYears(1));
        (result.To - result.From).TotalDays.Should().BeLessThanOrEqualTo(62);
    }

    [Test]
    public async Task DefaultsToTheCurrentCalendarMonth()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var result = await SendAsync(new GetBookingCalendarQuery());

        var now = DateTime.UtcNow;
        result.From.Should().Be(new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc));
        result.To.Should().Be(result.From.AddMonths(1));
    }
}
