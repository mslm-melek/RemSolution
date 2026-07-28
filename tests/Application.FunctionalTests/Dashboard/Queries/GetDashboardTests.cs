using RemSolution.Application.Common.Exceptions;
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
    public async Task MonthlySeriesIsContiguousAndEndsWithThePeriodMonth()
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

        var result = await SendAsync(new GetDashboardQuery(PeriodStart, PeriodEnd, MonthsOfHistory: 3));

        result.MonthlySeries.Should().HaveCount(3);
        // Oldest first, ending with the period's own month (April 2030).
        result.MonthlySeries.Select(p => (p.Year, p.Month))
            .Should().Equal((2030, 2), (2030, 3), (2030, 4));
        // Months with no activity are emitted as zeroes, not omitted.
        result.MonthlySeries[0].Collected!.Amount.Should().Be(0m);
        result.MonthlySeries[2].Collected!.Amount.Should().Be(300m);
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
