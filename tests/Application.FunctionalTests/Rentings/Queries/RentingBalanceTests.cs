using RemSolution.Application.Features.Payment.Commands.CreatePaymentCommand;
using RemSolution.Application.Features.Payment.Commands.ReversePaymentCommand;
using RemSolution.Application.Features.Renting.Commands.CreateRentingCommand;
using RemSolution.Application.Features.Renting.Queries.GetRentingByIdQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Rentings.Queries;

using static Testing;

// RentingDto carries what has been collected and what is still owed, so a list
// row can tell whether there is anything left to pay without reading the ledger
// (the renting list only offers its Pay action while something is outstanding).
public class RentingBalanceTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 4, 4, 0, 0, 0, DateTimeKind.Utc);

    // 3 days × 40 = 120 TND, ready to receive payment.
    private async Task<int> PricedRentingAsync(string matricule)
    {
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Payments, Enabled = true });

        var car = new Car { Matricule = matricule, Status = CarStatus.Active, DailyRate = Money.Of(40m, "TND") };
        await AddAsync(car);
        var client = new Client { FirstName = "Balance", LastName = "Client" };
        await AddAsync(client);

        return await SendAsync(new CreateRentingCommand
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End, StartMileage = 500
        });
    }

    [Test]
    public async Task AnUnpaidRentingOwesItsWholePrice()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var id = await PricedRentingAsync("BAL-1");

        var dto = await SendAsync(new GetRentingByIdQuery(id));

        dto!.Price!.Amount.Should().Be(120m);
        dto.Paid!.Amount.Should().Be(0m);
        dto.Outstanding!.Amount.Should().Be(120m);
        dto.Outstanding.Currency.Should().Be("TND");
    }

    [Test]
    public async Task PaymentsReduceTheOutstandingBalance()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var id = await PricedRentingAsync("BAL-2");

        await SendAsync(new CreatePaymentCommand { RentingId = id, Amount = 50m });

        var partly = await SendAsync(new GetRentingByIdQuery(id));
        partly!.Paid!.Amount.Should().Be(50m);
        partly.Outstanding!.Amount.Should().Be(70m);

        await SendAsync(new CreatePaymentCommand { RentingId = id, Amount = 70m });

        // Fully settled: nothing left, which is what hides the list's Pay action.
        var settled = await SendAsync(new GetRentingByIdQuery(id));
        settled!.Paid!.Amount.Should().Be(120m);
        settled.Outstanding!.Amount.Should().Be(0m);
    }

    [Test]
    public async Task AReversalPutsTheBalanceBack()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var id = await PricedRentingAsync("BAL-3");

        var paymentId = await SendAsync(new CreatePaymentCommand { RentingId = id, Amount = 120m });
        await SendAsync(new ReversePaymentCommand(paymentId));

        // The reversal is an offsetting negative entry, so the plain sum of the
        // entries is the true net collected — back to nothing paid.
        var dto = await SendAsync(new GetRentingByIdQuery(id));
        dto!.Paid!.Amount.Should().Be(0m);
        dto.Outstanding!.Amount.Should().Be(120m);
    }

    [Test]
    public async Task ARentingWithoutAPriceHasNoBalance()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Rentings, Enabled = true });

        // No daily rate and no agreed price: there is nothing to owe.
        var car = new Car { Matricule = "BAL-4", Status = CarStatus.Active };
        await AddAsync(car);
        var client = new Client { FirstName = "Priceless", LastName = "Client" };
        await AddAsync(client);

        var renting = new Renting
        {
            CarId = car.Id, ClientId = client.Id,
            StartDate = Start, EndDate = End,
            RentingState = RentingState.NotYet
        };
        await AddAsync(renting);

        var dto = await SendAsync(new GetRentingByIdQuery(renting.Id));

        dto!.Price.Should().BeNull();
        dto.Paid.Should().BeNull();
        dto.Outstanding.Should().BeNull();
    }
}
