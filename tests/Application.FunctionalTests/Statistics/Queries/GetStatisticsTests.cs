using RemSolution.Application.Features.Expense.Commands.CreateExpenseCommand;
using RemSolution.Application.Features.Payment.Commands.CreatePaymentCommand;
using RemSolution.Application.Features.Statistics.DTOs;
using RemSolution.Application.Features.Statistics.Queries.GetStatisticsQuery;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using ExpenseTypeEntity = RemSolution.Domain.Entities.ExpenseType;

namespace RemSolution.Application.FunctionalTests.Statistics.Queries;

using static Testing;

public class GetStatisticsTests : BaseTestFixture
{
    // A fixed year, so which bucket a figure lands in never depends on the day the
    // suite runs.
    private static readonly DateTime YearStart = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime YearEnd = new(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime InFebruary = new(2030, 2, 10, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime InApril = new(2030, 4, 10, 9, 0, 0, DateTimeKind.Utc);

    private async Task<Car> AddCarAsync(string matricule)
    {
        var car = new Car { Matricule = matricule, Status = CarStatus.Active };
        await AddAsync(car);
        return car;
    }

    private async Task<Client> AddClientAsync(string firstName)
    {
        var client = new Client { FirstName = firstName, LastName = "Stats" };
        await AddAsync(client);
        return client;
    }

    private async Task<Renting> AddHireAsync(
        Car car, Client client, DateTime start, int days, decimal price,
        RentingState state = RentingState.Done, decimal? cancellationFee = null)
    {
        var renting = new Renting
        {
            CarId = car.Id,
            ClientId = client.Id,
            StartDate = start,
            EndDate = start.AddDays(days),
            RentingState = state,
            Price = Money.Of(price, "TND"),
            CancellationFee = cancellationFee == null ? null : Money.Of(cancellationFee.Value, "TND")
        };
        await AddAsync(renting);
        return renting;
    }

    private static async Task<int> AddExpenseAsync(Car car, DateTime on, decimal amount, string typeName)
    {
        var type = new ExpenseTypeEntity { Name = typeName, IsActive = true };
        await AddAsync(type);
        return await SendAsync(new CreateExpenseCommand
        {
            CarId = car.Id, ExpenseTypeId = type.Id, ExpenseDate = on, Amount = amount
        });
    }

    [Test]
    public async Task ListsEveryMonthOfTheYearAndPutsAHireInTheMonthItStarts()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = await AddCarAsync("ST-1");
        var client = await AddClientAsync("Month");

        await AddHireAsync(car, client, InFebruary, days: 3, price: 300m);

        var result = await SendAsync(new GetStatisticsQuery(
            From: YearStart, To: YearEnd, Granularity: StatisticsGranularity.Month));

        result.From.Should().Be(YearStart);
        result.To.Should().Be(YearEnd);
        result.Truncated.Should().BeFalse();
        result.Periods.Should().HaveCount(12, "an empty month is still a row");

        var february = result.Periods.Single(p => p.BucketStart!.Value.Month == 2);
        february.Rentings.Should().Be(1);
        february.RentedDays.Should().Be(3);
        february.Charged!.Amount.Should().Be(300m);

        result.Periods.Where(p => p.BucketStart!.Value.Month != 2)
            .Should().OnlyContain(p => p.Rentings == 0 && p.Charged!.Amount == 0m);

        result.Totals.Rentings.Should().Be(1);
        result.Totals.RentedDays.Should().Be(3);
        result.Totals.Charged!.Amount.Should().Be(300m);
        result.Currency.Should().Be("TND");
    }

    [Test]
    public async Task AHireBelongsWholeToTheMonthItStartsInEvenWhenItSpansTwo()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = await AddCarAsync("ST-SPAN");
        var client = await AddClientAsync("Span");

        // Out on 25 February, back on 6 March: ten days, all of them February's.
        await AddHireAsync(car, client, new DateTime(2030, 2, 25, 0, 0, 0, DateTimeKind.Utc),
            days: 10, price: 1000m);

        var result = await SendAsync(new GetStatisticsQuery(
            From: YearStart, To: YearEnd, Granularity: StatisticsGranularity.Month));

        var february = result.Periods.Single(p => p.BucketStart!.Value.Month == 2);
        february.RentedDays.Should().Be(10);
        february.Charged!.Amount.Should().Be(1000m);

        var march = result.Periods.Single(p => p.BucketStart!.Value.Month == 3);
        march.Rentings.Should().Be(0);
        march.RentedDays.Should().Be(0);
        march.Charged!.Amount.Should().Be(0m);
    }

    [Test]
    public async Task MoneyMovesAreDatedByWhenTheyMovedAndTheResultIsBilledMinusSpent()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = await AddCarAsync("ST-MONEY");
        var client = await AddClientAsync("Money");

        var hire = await AddHireAsync(car, client, InFebruary, days: 2, price: 400m);

        // Paid the month after the hire: billed in February, collected in March. The
        // payment names the hire (a payment has exactly one target), which is how it
        // gets attributed to the car.
        await SendAsync(new CreatePaymentCommand
        {
            RentingId = hire.Id, Amount = 250m,
            PayementDate = new DateTime(2030, 3, 4, 0, 0, 0, DateTimeKind.Utc)
        });

        await AddExpenseAsync(car, InApril, 100m, "Tyres");

        var result = await SendAsync(new GetStatisticsQuery(
            From: YearStart, To: YearEnd, Granularity: StatisticsGranularity.Month));

        var february = result.Periods.Single(p => p.BucketStart!.Value.Month == 2);
        february.Charged!.Amount.Should().Be(400m);
        february.Collected!.Amount.Should().Be(0m);
        february.Net!.Amount.Should().Be(400m);

        var march = result.Periods.Single(p => p.BucketStart!.Value.Month == 3);
        march.Charged!.Amount.Should().Be(0m);
        march.Collected!.Amount.Should().Be(250m);

        var april = result.Periods.Single(p => p.BucketStart!.Value.Month == 4);
        april.Expenses!.Amount.Should().Be(100m);
        april.Net!.Amount.Should().Be(-100m);

        result.Totals.Charged!.Amount.Should().Be(400m);
        result.Totals.Collected!.Amount.Should().Be(250m);
        result.Totals.Expenses!.Amount.Should().Be(100m);
        result.Totals.Net!.Amount.Should().Be(300m);
    }

    [Test]
    public async Task ACancelledHireBillsTheFeeKeptAndCountsNoRentingOrDays()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = await AddCarAsync("ST-CANCEL");
        var client = await AddClientAsync("Cancel");

        await AddHireAsync(car, client, InFebruary, days: 4, price: 500m,
            state: RentingState.Cancelled, cancellationFee: 75m);

        var result = await SendAsync(new GetStatisticsQuery(
            From: YearStart, To: YearEnd, Granularity: StatisticsGranularity.Month));

        var february = result.Periods.Single(p => p.BucketStart!.Value.Month == 2);
        february.Rentings.Should().Be(0);
        february.RentedDays.Should().Be(0);
        february.Charged!.Amount.Should().Be(75m, "a cancelled hire bills only what the agency kept");
    }

    [Test]
    public async Task BreaksTheFleetDownPerCarAndTheRowsAddUpToTheTotals()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var earner = await AddCarAsync("ST-EARN");
        var idle = await AddCarAsync("ST-IDLE");
        var client = await AddClientAsync("Fleet");

        await AddHireAsync(earner, client, InFebruary, days: 5, price: 600m);
        await AddExpenseAsync(idle, InApril, 80m, "Repair");

        var result = await SendAsync(new GetStatisticsQuery(
            From: YearStart, To: YearEnd, Granularity: StatisticsGranularity.Month));

        result.ByCar.Should().HaveCount(2, "a car that earned nothing is worth a row too");
        result.Cars.Should().HaveCount(2);

        var earnerRow = result.ByCar.Single(r => r.CarId == earner.Id);
        earnerRow.Matricule.Should().Be("ST-EARN");
        earnerRow.Rentings.Should().Be(1);
        earnerRow.RentedDays.Should().Be(5);
        earnerRow.Charged!.Amount.Should().Be(600m);
        earnerRow.Net!.Amount.Should().Be(600m);

        var idleRow = result.ByCar.Single(r => r.CarId == idle.Id);
        idleRow.Rentings.Should().Be(0);
        idleRow.Expenses!.Amount.Should().Be(80m);
        idleRow.Net!.Amount.Should().Be(-80m);

        // Best result first.
        result.ByCar.First().CarId.Should().Be(earner.Id);

        result.ByCar.Sum(r => r.Charged!.Amount).Should().Be(result.Totals.Charged!.Amount);
        result.ByCar.Sum(r => r.Expenses!.Amount).Should().Be(result.Totals.Expenses!.Amount);
        result.ByCar.Sum(r => r.Rentings).Should().Be(result.Totals.Rentings);
    }

    [Test]
    public async Task FilteringToOneCarLeavesTheOtherCarsFiguresOutButKeepsThePicker()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var mine = await AddCarAsync("ST-MINE");
        var other = await AddCarAsync("ST-OTHER");
        var client = await AddClientAsync("Filter");

        await AddHireAsync(mine, client, InFebruary, days: 2, price: 200m);
        await AddHireAsync(other, client, InFebruary, days: 9, price: 900m);
        await AddExpenseAsync(other, InFebruary, 300m, "Service");

        var result = await SendAsync(new GetStatisticsQuery(
            CarId: mine.Id, From: YearStart, To: YearEnd,
            Granularity: StatisticsGranularity.Month));

        result.CarId.Should().Be(mine.Id);
        result.CarLabel.Should().Contain("ST-MINE");
        result.Totals.Rentings.Should().Be(1);
        result.Totals.RentedDays.Should().Be(2);
        result.Totals.Charged!.Amount.Should().Be(200m);
        result.Totals.Expenses!.Amount.Should().Be(0m);

        result.ByCar.Should().BeEmpty("the period rows already are this car's breakdown");
        result.Cars.Should().HaveCount(2, "the picker still has to offer the other car");
    }

    [Test]
    public async Task YearGranularityGivesOneRowPerYear()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = await AddCarAsync("ST-YEAR");
        var client = await AddClientAsync("Year");

        await AddHireAsync(car, client, InFebruary, days: 3, price: 300m);
        await AddHireAsync(car, client, InFebruary.AddYears(1), days: 1, price: 100m);

        var result = await SendAsync(new GetStatisticsQuery(
            From: YearStart, To: new DateTime(2032, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Granularity: StatisticsGranularity.Year));

        result.Periods.Should().HaveCount(2);
        result.Periods[0].BucketStart!.Value.Year.Should().Be(2030);
        result.Periods[0].Charged!.Amount.Should().Be(300m);
        result.Periods[1].BucketStart!.Value.Year.Should().Be(2031);
        result.Periods[1].Charged!.Amount.Should().Be(100m);
        result.Totals.Charged!.Amount.Should().Be(400m);
    }

    [Test]
    public async Task AWindowLongerThanTheCeilingKeepsTheLatestRowsAndSaysSo()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        // Five years by month is sixty buckets; the query answers with the last 36.
        var result = await SendAsync(new GetStatisticsQuery(
            From: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            To: new DateTime(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Granularity: StatisticsGranularity.Month));

        result.Truncated.Should().BeTrue();
        result.Periods.Should().HaveCount(36);
        result.To.Should().Be(new DateTime(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        result.From.Should().Be(new DateTime(2028, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public async Task AbsurdBoundsAnswerARowInsteadOfFalling()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        // A hand-edited URL naming the ends of the calendar: stepping a month off
        // either end would throw, so the bounds are pulled back to ones the report
        // can express.
        var result = await SendAsync(new GetStatisticsQuery(
            From: DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
            To: DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc),
            Granularity: StatisticsGranularity.Month));

        result.Truncated.Should().BeTrue();
        result.Periods.Should().HaveCount(36);
    }

    [Test]
    public async Task PartMonthBoundsAreWidenedToWholeMonths()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = await AddCarAsync("ST-BOUNDS");
        var client = await AddClientAsync("Bounds");

        // Started on the 2nd, before the requested 10th: the row is the whole month,
        // so the hire is in it.
        await AddHireAsync(car, client, new DateTime(2030, 2, 2, 0, 0, 0, DateTimeKind.Utc),
            days: 1, price: 100m);

        var result = await SendAsync(new GetStatisticsQuery(
            From: new DateTime(2030, 2, 10, 0, 0, 0, DateTimeKind.Utc),
            To: new DateTime(2030, 2, 20, 0, 0, 0, DateTimeKind.Utc),
            Granularity: StatisticsGranularity.Month));

        result.From.Should().Be(new DateTime(2030, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        result.To.Should().Be(new DateTime(2030, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        result.Periods.Should().ContainSingle();
        result.Totals.Charged!.Amount.Should().Be(100m);
    }
}
