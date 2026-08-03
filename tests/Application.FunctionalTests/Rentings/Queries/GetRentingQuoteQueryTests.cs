using RemSolution.Application.Features.Renting.Commands.CreateRentingCommand;
using RemSolution.Application.Features.Renting.Queries.GetRentingQuoteQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Rentings.Queries;

using static Testing;

/// <summary>
/// The figure the booking screen shows before anything is written. Its promise is
/// that it matches what CreateRentingCommand would store, so the two are asserted
/// against the same numbers here.
/// </summary>
public class GetRentingQuoteQueryTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 9, 4, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ShouldPriceThePeriodTheSameWayCreatingItWould()
    {
        await SetUpAgencyAsync();
        var carId = await SeedCarAsync("QUOTE-1", dailyRate: 80m);

        var quote = await SendAsync(new GetRentingQuoteQuery(carId, Start, End));

        quote.BilledDays.Should().Be(3);
        quote.DailyRate!.Amount.Should().Be(80m);
        quote.Price!.Amount.Should().Be(240m);
        quote.Currency.Should().Be("TND");
        quote.IsCarBookable.Should().BeTrue();
        quote.IsAvailable.Should().BeTrue();

        // The promise: booking it stores exactly what was quoted.
        var client = new Client { FirstName = "Test", LastName = "Client" };
        await AddAsync(client);

        var id = await SendAsync(new CreateRentingCommand
        {
            CarId = carId, ClientId = client.Id, StartDate = Start, EndDate = End
        });

        (await FindAsync<Renting>(id))!.Price!.Amount.Should().Be(quote.Price.Amount);
    }

    [Test]
    public async Task ShouldReportAPeriodTheCarIsAlreadyTakenFor()
    {
        await SetUpAgencyAsync();
        var carId = await SeedCarAsync("QUOTE-2", dailyRate: 80m);

        var client = new Client { FirstName = "Test", LastName = "Client" };
        await AddAsync(client);

        await SendAsync(new CreateRentingCommand
        {
            CarId = carId, ClientId = client.Id, StartDate = Start, EndDate = End
        });

        // Overlaps the booking above — reported, not thrown: nothing was attempted.
        var quote = await SendAsync(new GetRentingQuoteQuery(carId, Start.AddDays(1), End.AddDays(1)));

        quote.IsAvailable.Should().BeFalse();
        quote.Price!.Amount.Should().Be(240m);
    }

    [Test]
    public async Task ShouldStillQuoteACarWithNoRateSoAPriceCanBeTyped()
    {
        await SetUpAgencyAsync();
        var carId = await SeedCarAsync("QUOTE-3", dailyRate: null);

        var quote = await SendAsync(new GetRentingQuoteQuery(carId, Start, End));

        quote.DailyRate.Should().BeNull();
        quote.Price.Should().BeNull();
        quote.BilledDays.Should().Be(3);
        // Still says which currency a typed price would be in.
        quote.Currency.Should().Be("TND");
    }

    [Test]
    public async Task ShouldTreatAnUnfinishedPeriodAsNothingToPrice()
    {
        await SetUpAgencyAsync();
        var carId = await SeedCarAsync("QUOTE-4", dailyRate: 80m);

        // The agent is still typing: end before start is an ordinary state of the
        // form, so the query answers rather than failing.
        var quote = await SendAsync(new GetRentingQuoteQuery(carId, End, Start));

        quote.BilledDays.Should().Be(0);
        quote.Price.Should().BeNull();
        quote.Currency.Should().Be("TND");
    }

    [Test]
    public async Task ShouldFlagACarThatCannotBeBookedAtAll()
    {
        await SetUpAgencyAsync();

        var car = new Car
        {
            Matricule = "QUOTE-5",
            Status = CarStatus.Maintenance,
            DailyRate = Money.Of(80m, "TND")
        };
        await AddAsync(car);

        var quote = await SendAsync(new GetRentingQuoteQuery(car.Id, Start, End));

        quote.IsCarBookable.Should().BeFalse();
    }

    private static async Task SetUpAgencyAsync()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });
    }

    private static async Task<int> SeedCarAsync(string matricule, decimal? dailyRate)
    {
        var car = new Car
        {
            Matricule = matricule,
            Status = CarStatus.Active,
            DailyRate = dailyRate is decimal r ? Money.Of(r, "TND") : null,
        };
        await AddAsync(car);
        return car.Id;
    }
}
