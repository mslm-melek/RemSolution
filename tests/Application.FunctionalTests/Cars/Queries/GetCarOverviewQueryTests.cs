using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Car.Queries.GetCarOverviewQuery;
using RemSolution.Application.Features.Expense.Commands.CreateExpenseCommand;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using ExpenseTypeEntity = RemSolution.Domain.Entities.ExpenseType;

namespace RemSolution.Application.FunctionalTests.Cars.Queries;

using static Testing;

/// <summary>
/// The figures on one car's page. What matters here is that the two readings of
/// the same hires stay apart — occupancy counts the DAYS THE CAR WAS OUT, money
/// follows the statistics report's rule of attributing a hire whole to the period
/// it STARTS in — and that a car nobody can see is still a 404.
/// </summary>
public class GetCarOverviewQueryTests : BaseTestFixture
{
    [Test]
    public async Task ShouldCountDaysTheCarWasOutRatherThanHires()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = new Car { Matricule = "OV-USE", Status = CarStatus.Active };
        await AddAsync(car);

        var today = DateTime.UtcNow.Date;

        // Ten whole days inside the window, finished.
        await AddAsync(new Renting
        {
            CarId = car.Id,
            StartDate = today.AddDays(-20),
            EndDate = today.AddDays(-10),
            RentingState = RentingState.Done,
            Price = Money.Of(1000m, "TND"),
        });

        // A cancelled hire never held the car and never billed the price.
        await AddAsync(new Renting
        {
            CarId = car.Id,
            StartDate = today.AddDays(-8),
            EndDate = today.AddDays(-5),
            RentingState = RentingState.Cancelled,
            Price = Money.Of(500m, "TND"),
            CancellationFee = Money.Of(50m, "TND"),
        });

        var overview = await SendAsync(new GetCarOverviewQuery(car.Id));

        overview.Usage.Should().NotBeNull();
        overview.Usage!.WindowDays.Should().Be(90);
        overview.Usage.RentedDays.Should().Be(10);
        // 10 / 90, rounded.
        overview.Usage.UtilizationPercent.Should().Be(11);
        // Only the live hire is counted as one…
        overview.Usage.Rentings.Should().Be(1);
        // …but the cancelled one still bills the fee that was kept.
        overview.Usage.Charged!.Amount.Should().Be(1050m);
    }

    // The rule that keeps a percentage a percentage: two bookings touching the
    // same day are one day out, not two.
    [Test]
    public async Task ShouldNotCountAnOverlappingDayTwice()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = new Car { Matricule = "OV-LAP", Status = CarStatus.Active };
        await AddAsync(car);

        var today = DateTime.UtcNow.Date;

        await AddAsync(new Renting
        {
            CarId = car.Id,
            StartDate = today.AddDays(-10),
            EndDate = today.AddDays(-5),
            RentingState = RentingState.Done,
        });
        await AddAsync(new Renting
        {
            CarId = car.Id,
            StartDate = today.AddDays(-7),
            EndDate = today.AddDays(-3),
            RentingState = RentingState.Done,
        });

        var overview = await SendAsync(new GetCarOverviewQuery(car.Id));

        // Days -10 through -4 inclusive: seven, not the eleven the two hires add
        // up to on their own.
        overview.Usage!.RentedDays.Should().Be(7);
    }

    // A hire that began before the window still occupies its tail of it — the
    // occupancy half deliberately does NOT use the start-date attribution the
    // money half does.
    [Test]
    public async Task ShouldCountTheTailOfAHireThatStartedBeforeTheWindow()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = new Car { Matricule = "OV-TAIL", Status = CarStatus.Active };
        await AddAsync(car);

        var today = DateTime.UtcNow.Date;

        await AddAsync(new Renting
        {
            CarId = car.Id,
            StartDate = today.AddDays(-120),
            EndDate = today.AddDays(-85),
            RentingState = RentingState.Done,
            Price = Money.Of(400m, "TND"),
        });

        var overview = await SendAsync(new GetCarOverviewQuery(car.Id));

        // The window opens 89 days back (it ends at the end of today), so the
        // hire's last four days fall inside it.
        overview.Usage!.RentedDays.Should().Be(4);
        // The money is dated by the hire's start, which is outside — the figure
        // belongs to the quarter it began in, exactly as the report says.
        overview.Usage.Charged!.Amount.Should().Be(0m);
    }

    [Test]
    public async Task ShouldListTheRunningHireBeforeTheOnesBookedAfterIt()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = new Car { Matricule = "OV-BOOK", Status = CarStatus.Active };
        await AddAsync(car);

        var client = new Client { FirstName = "Ahmed", LastName = "Ben Salem" };
        await AddAsync(client);

        var today = DateTime.UtcNow.Date;

        await AddAsync(new Renting
        {
            CarId = car.Id,
            ClientId = client.Id,
            StartDate = today.AddDays(5),
            EndDate = today.AddDays(8),
            RentingState = RentingState.NotYet,
        });
        await AddAsync(new Renting
        {
            CarId = car.Id,
            ClientId = client.Id,
            // Due back yesterday and still out: late.
            StartDate = today.AddDays(-3),
            EndDate = today.AddDays(-1),
            RentingState = RentingState.InProgress,
        });
        // History: neither current nor upcoming, so it belongs to the page's
        // table rather than this list — but it still counts towards the total.
        await AddAsync(new Renting
        {
            CarId = car.Id,
            ClientId = client.Id,
            StartDate = today.AddDays(-30),
            EndDate = today.AddDays(-28),
            RentingState = RentingState.Done,
        });

        var overview = await SendAsync(new GetCarOverviewQuery(car.Id));

        overview.Bookings.Should().HaveCount(2);
        overview.Bookings![0].State.Should().Be(RentingState.InProgress);
        overview.Bookings[0].IsLate.Should().BeTrue();
        overview.Bookings[0].ClientName.Should().Be("Ahmed Ben Salem");
        overview.Bookings[1].State.Should().Be(RentingState.NotYet);
        overview.Bookings[1].IsLate.Should().BeFalse();

        // The link under the list leads to every hire the car has had.
        overview.BookingsTotal.Should().Be(3);
    }

    [Test]
    public async Task ShouldListRecentExpensesAndTotalTheWindow()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = new Car { Matricule = "OV-COST", Status = CarStatus.Active };
        await AddAsync(car);

        var type = new ExpenseTypeEntity { Name = "Garage", IsActive = true };
        await AddAsync(type);

        var today = DateTime.UtcNow.Date;

        await SendAsync(new CreateExpenseCommand
        {
            CarId = car.Id, ExpenseTypeId = type.Id,
            ExpenseDate = today.AddDays(-2), Amount = 95m, PaidAmount = 0m
        });
        await SendAsync(new CreateExpenseCommand
        {
            CarId = car.Id, ExpenseTypeId = type.Id,
            ExpenseDate = today.AddDays(-10), Amount = 65m, PaidAmount = 65m
        });
        // Older than the window: listed (it is one of the five most recent), but
        // not part of the window's total.
        await SendAsync(new CreateExpenseCommand
        {
            CarId = car.Id, ExpenseTypeId = type.Id,
            ExpenseDate = today.AddDays(-200), Amount = 1000m, PaidAmount = 1000m
        });

        var overview = await SendAsync(new GetCarOverviewQuery(car.Id));

        overview.Expenses.Should().HaveCount(3);
        overview.Expenses![0].Amount!.Amount.Should().Be(95m);
        overview.Expenses[0].TypeName.Should().Be("Garage");
        overview.Expenses[0].IsUnpaid.Should().BeTrue();
        overview.Expenses[1].IsUnpaid.Should().BeFalse();

        overview.ExpensesTotal!.Amount.Should().Be(160m);
    }

    [Test]
    public async Task ShouldAverageOnlyTheReviewsLeftOnThisCar()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        var car = new Car { Matricule = "OV-STAR", Status = CarStatus.Active };
        var other = new Car { Matricule = "OV-STAR-2", Status = CarStatus.Active };
        await AddAsync(car);
        await AddAsync(other);

        await AddReviewAsync(agencyId, car.Id, 5);
        await AddReviewAsync(agencyId, car.Id, 4);
        // Another car's review must not lift this one's score.
        await AddReviewAsync(agencyId, other.Id, 1);

        var overview = await SendAsync(new GetCarOverviewQuery(car.Id));

        overview.Rating.Count.Should().Be(2);
        overview.Rating.Average.Should().Be(4.5);
    }

    // A car nobody has rated has no score to show — which is not a score of zero.
    [Test]
    public async Task ShouldReportNoAverageForAnUnratedCar()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = new Car { Matricule = "OV-NOSTAR", Status = CarStatus.Active };
        await AddAsync(car);

        var overview = await SendAsync(new GetCarOverviewQuery(car.Id));

        overview.Rating.Count.Should().Be(0);
        overview.Rating.Average.Should().BeNull();
    }

    [Test]
    public async Task ShouldNotFindACarInAnotherAgency()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = new Car { Matricule = "OV-MINE", Status = CarStatus.Active };
        await AddAsync(car);

        // Move the caller into a second agency: the plate exists, but not for
        // them, and this endpoint must not be the one that says otherwise.
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() => SendAsync(new GetCarOverviewQuery(car.Id)))
            .Should().ThrowAsync<NotFoundException>();
    }

    // Seeds a review the way a marketplace customer leaves one: hanging off a
    // finished renting of the car, with the agency named explicitly (AgencyReview
    // is platform-level and carries no tenant filter).
    private static async Task AddReviewAsync(int agencyId, int carId, int rating)
    {
        var renting = new Renting { CarId = carId, RentingState = RentingState.Done };
        await AddAsync(renting);

        await AddAsync(new AgencyReview
        {
            AgencyId = agencyId,
            RentingId = renting.Id,
            Rating = rating,
            AuthorName = "Someone",
            SubmittedAt = DateTime.UtcNow,
        });
    }
}
