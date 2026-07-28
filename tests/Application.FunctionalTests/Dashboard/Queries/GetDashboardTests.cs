using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Dashboard.DTOs;
using RemSolution.Application.Features.Dashboard.Queries.GetDashboardQuery;
using RemSolution.Application.Features.Expense.Commands.CreateExpenseCommand;
using RemSolution.Application.Features.Payment.Commands.CreatePaymentCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using ExpenseTypeEntity = RemSolution.Domain.Entities.ExpenseType;

namespace RemSolution.Application.FunctionalTests.Dashboard.Queries;

using static Testing;

public class GetDashboardTests : BaseTestFixture
{
    // A fixed window, so the figures never depend on the day the suite runs.
    private static readonly DateTime PeriodStart = new(2030, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEnd = new(2030, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime InPeriod = new(2030, 4, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime OutOfPeriod = new(2030, 2, 10, 0, 0, 0, DateTimeKind.Utc);

    private async Task<Car> AddCarAsync(string matricule, CarStatus status = CarStatus.Active)
    {
        var car = new Car { Matricule = matricule, Status = status };
        await AddAsync(car);
        return car;
    }

    [Test]
    public async Task CountsFleetAndBookings()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var onRent = await AddCarAsync("DB-1");
        await AddCarAsync("DB-2");
        await AddCarAsync("DB-3", CarStatus.Maintenance);

        var client = new Client { FirstName = "Dash", LastName = "Client" };
        await AddAsync(client);

        await AddAsync(new Renting
        {
            CarId = onRent.Id, ClientId = client.Id, StartDate = InPeriod, EndDate = InPeriod.AddDays(3),
            RentingState = RentingState.InProgress, Price = Money.Of(150m, "TND")
        });
        await AddAsync(new Renting
        {
            CarId = onRent.Id, ClientId = client.Id, StartDate = InPeriod.AddDays(20),
            EndDate = InPeriod.AddDays(22), RentingState = RentingState.NotYet, Price = Money.Of(100m, "TND")
        });

        var result = await SendAsync(new GetDashboardQuery(PeriodStart, PeriodEnd));

        result.TotalCars.Should().Be(3);
        result.ActiveCars.Should().Be(2);
        result.CarsOnRent.Should().Be(1);
        result.RentingsInProgress.Should().Be(1);
        result.RentingsUpcoming.Should().Be(1);
        result.TotalClients.Should().Be(1);
        // The ongoing renting ends inside the window.
        result.ReturnsDueInPeriod.Should().Be(1);
    }

    [Test]
    public async Task MoneyFiguresCoverOnlyTheRequestedWindow()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = await AddCarAsync("DB-MONEY");
        var client = new Client { FirstName = "Money", LastName = "Client" };
        await AddAsync(client);

        // Charged: one renting inside the window, one outside it.
        await AddAsync(new Renting
        {
            CarId = car.Id, ClientId = client.Id, StartDate = InPeriod, EndDate = InPeriod.AddDays(2),
            RentingState = RentingState.InProgress, Price = Money.Of(200m, "TND")
        });
        await AddAsync(new Renting
        {
            CarId = car.Id, ClientId = client.Id, StartDate = OutOfPeriod, EndDate = OutOfPeriod.AddDays(2),
            RentingState = RentingState.Done, Price = Money.Of(500m, "TND")
        });

        // Collected: one payment inside, one outside.
        await SendAsync(new CreatePaymentCommand
        {
            ClientId = client.Id, Amount = 120m, PayementDate = InPeriod
        });
        await SendAsync(new CreatePaymentCommand
        {
            ClientId = client.Id, Amount = 90m, PayementDate = OutOfPeriod
        });

        // Spent: one expense inside, one outside.
        var type = new ExpenseTypeEntity { Name = "Fuel", IsActive = true };
        await AddAsync(type);
        await SendAsync(new CreateExpenseCommand
        {
            CarId = car.Id, ExpenseTypeId = type.Id, ExpenseDate = InPeriod, Amount = 50m
        });
        await SendAsync(new CreateExpenseCommand
        {
            CarId = car.Id, ExpenseTypeId = type.Id, ExpenseDate = OutOfPeriod, Amount = 999m
        });

        var result = await SendAsync(new GetDashboardQuery(PeriodStart, PeriodEnd));

        result.ChargedInPeriod!.Amount.Should().Be(200m);
        result.CollectedInPeriod!.Amount.Should().Be(120m);
        result.ExpensesInPeriod!.Amount.Should().Be(50m);
        result.NetInPeriod!.Amount.Should().Be(70m); // 120 collected − 50 spent
    }

    [Test]
    public async Task OutstandingFiguresAreAllTimeNotPeriodScoped()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = await AddCarAsync("DB-DEBT");
        var client = new Client { FirstName = "Owing", LastName = "Client" };
        await AddAsync(client);

        // Charged outside the window and unpaid: still counts as owed.
        await AddAsync(new Renting
        {
            CarId = car.Id, ClientId = client.Id, StartDate = OutOfPeriod, EndDate = OutOfPeriod.AddDays(2),
            RentingState = RentingState.Done, Price = Money.Of(400m, "TND")
        });
        await SendAsync(new CreatePaymentCommand
        {
            ClientId = client.Id, Amount = 100m, PayementDate = OutOfPeriod
        });

        var type = new ExpenseTypeEntity { Name = "Tyres", IsActive = true };
        await AddAsync(type);
        await SendAsync(new CreateExpenseCommand
        {
            CarId = car.Id, ExpenseTypeId = type.Id, ExpenseDate = OutOfPeriod, Amount = 200m, PaidAmount = 50m
        });

        var result = await SendAsync(new GetDashboardQuery(PeriodStart, PeriodEnd));

        result.ClientsOutstanding!.Amount.Should().Be(300m);
        result.ClientsInDebtCount.Should().Be(1);
        result.ExpensesOutstanding!.Amount.Should().Be(150m);
    }

    [Test]
    public async Task PendingReservationRequestsAreCounted()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = await AddCarAsync("DB-REQ");
        await AddAsync(Reservation.Create(
            car.Id, InPeriod, InPeriod.AddDays(2), price: Money.Of(80m, "TND"),
            expiresAt: InPeriod.AddHours(24)));

        var result = await SendAsync(new GetDashboardQuery(PeriodStart, PeriodEnd));

        result.PendingReservationRequests.Should().Be(1);
    }

    [Test]
    public async Task SeriesIsContiguousAndEndsWithThePeriodMonth()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = await AddCarAsync("DB-SERIES");
        var client = new Client { FirstName = "Series", LastName = "Client" };
        await AddAsync(client);
        await AddAsync(new Renting
        {
            CarId = car.Id, ClientId = client.Id, StartDate = InPeriod, EndDate = InPeriod.AddDays(2),
            RentingState = RentingState.InProgress, Price = Money.Of(300m, "TND")
        });
        await SendAsync(new CreatePaymentCommand
        {
            ClientId = client.Id, Amount = 300m, PayementDate = InPeriod
        });

        var result = await SendAsync(new GetDashboardQuery(PeriodStart, PeriodEnd, Periods: 3));

        result.Granularity.Should().Be(DashboardGranularity.Month);
        result.Series.Should().HaveCount(3);
        // Oldest first, ending with the period's own month (April 2030).
        result.Series.Select(p => (p.BucketStart.Year, p.BucketStart.Month))
            .Should().Equal((2030, 2), (2030, 3), (2030, 4));
        // Buckets are half-open and step by a calendar month.
        result.Series[0].BucketEnd.Should().Be(new DateTime(2030, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        // Months with no activity are emitted as zeroes, not omitted.
        result.Series[0].Collected!.Amount.Should().Be(0m);
        result.Series[2].Collected!.Amount.Should().Be(300m);
        // The renting started in April, so the activity count lands there too.
        result.Series[2].RentingsStarted.Should().Be(1);
    }

    [Test]
    public async Task SeriesEndsWithTheLastMonthOfAMultiMonthWindow()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        // A quarter: February through April 2030, half-open.
        var quarterStart = new DateTime(2030, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var quarterEnd = new DateTime(2030, 5, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = await SendAsync(new GetDashboardQuery(quarterStart, quarterEnd, Periods: 3));

        // The chart must end on the month the figures above it end on (April),
        // not on the month the window opened (February).
        result.Series.Select(p => (p.BucketStart.Year, p.BucketStart.Month))
            .Should().Equal((2030, 2), (2030, 3), (2030, 4));
    }

    [Test]
    public async Task YearGranularityFoldsEveryMonthOfAYearIntoOneBucket()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = await AddCarAsync("DB-YEAR");
        var client = new Client { FirstName = "Year", LastName = "Client" };
        await AddAsync(client);

        // Two payments in different months of the same year, one in the year before.
        await SendAsync(new CreatePaymentCommand
        {
            ClientId = client.Id, Amount = 100m, PayementDate = new DateTime(2030, 1, 15, 0, 0, 0, DateTimeKind.Utc)
        });
        await SendAsync(new CreatePaymentCommand
        {
            ClientId = client.Id, Amount = 250m, PayementDate = new DateTime(2030, 9, 3, 0, 0, 0, DateTimeKind.Utc)
        });
        await SendAsync(new CreatePaymentCommand
        {
            ClientId = client.Id, Amount = 40m, PayementDate = new DateTime(2029, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        var result = await SendAsync(new GetDashboardQuery(
            PeriodStart, PeriodEnd, Periods: 2, Granularity: DashboardGranularity.Year));

        result.Granularity.Should().Be(DashboardGranularity.Year);
        result.Series.Select(p => p.BucketStart.Year).Should().Equal(2029, 2030);
        result.Series[0].Collected!.Amount.Should().Be(40m);
        // January and September of 2030 collapse into the one bucket.
        result.Series[1].Collected!.Amount.Should().Be(350m);
        result.Series[1].BucketEnd.Should().Be(new DateTime(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public async Task DayGranularitySeparatesConsecutiveDays()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var client = new Client { FirstName = "Daily", LastName = "Client" };
        await AddAsync(client);

        await SendAsync(new CreatePaymentCommand
        {
            ClientId = client.Id, Amount = 70m, PayementDate = new DateTime(2030, 4, 29, 9, 0, 0, DateTimeKind.Utc)
        });
        await SendAsync(new CreatePaymentCommand
        {
            ClientId = client.Id, Amount = 30m, PayementDate = new DateTime(2030, 4, 30, 18, 0, 0, DateTimeKind.Utc)
        });

        // Three days ending with the window's last day (30 April — the window is
        // half-open, so 1 May is not in it).
        var result = await SendAsync(new GetDashboardQuery(
            PeriodStart, PeriodEnd, Periods: 3, Granularity: DashboardGranularity.Day));

        result.Series.Select(p => p.BucketStart.Day).Should().Equal(28, 29, 30);
        result.Series[0].Collected!.Amount.Should().Be(0m);
        // Time of day does not smear a payment into the neighbouring bucket.
        result.Series[1].Collected!.Amount.Should().Be(70m);
        result.Series[2].Collected!.Amount.Should().Be(30m);
    }

    [Test]
    public async Task SeriesCountsCarsAndClientsAddedInEachBucket()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await AddCarAsync("DB-NEW-1");
        await AddCarAsync("DB-NEW-2");
        await AddAsync(new Client { FirstName = "Fresh", LastName = "Client" });

        // Rows are stamped as they are written, so they land in the bucket holding
        // "now" — the window is anchored on it rather than on the fixed test dates.
        var result = await SendAsync(new GetDashboardQuery(Granularity: DashboardGranularity.Month));

        var current = result.Series.Last();
        current.NewCars.Should().Be(2);
        current.NewClients.Should().Be(1);
        result.NewCarsInPeriod.Should().Be(2);
        result.NewClientsInPeriod.Should().Be(1);
    }

    [Test]
    public async Task TheBucketCountIsCappedPerGranularity()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        // A chart cannot show a thousand points; the ceiling is per granularity.
        (await SendAsync(new GetDashboardQuery(PeriodStart, PeriodEnd, Periods: 1000)))
            .Series.Should().HaveCount(24);
        (await SendAsync(new GetDashboardQuery(
                PeriodStart, PeriodEnd, Periods: 1000, Granularity: DashboardGranularity.Year)))
            .Series.Should().HaveCount(10);
        (await SendAsync(new GetDashboardQuery(
                PeriodStart, PeriodEnd, Periods: 1000, Granularity: DashboardGranularity.Day)))
            .Series.Should().HaveCount(90);
        // And never fewer than one.
        (await SendAsync(new GetDashboardQuery(PeriodStart, PeriodEnd, Periods: 0)))
            .Series.Should().HaveCount(1);
    }

    [Test]
    public async Task FiguresAreScopedToTheCurrentAgency()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddCarAsync("DB-HIDDEN");

        await AddTestAgencyAsync(); // second tenant

        var result = await SendAsync(new GetDashboardQuery(PeriodStart, PeriodEnd));

        result.TotalCars.Should().Be(0);
    }

    [Test]
    public async Task StaffWithoutTheDashboardPermissionIsDenied()
    {
        await RunAsAgencyStaffAsync(Permissions.CarRead);
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() => SendAsync(new GetDashboardQuery(PeriodStart, PeriodEnd)))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }
}
