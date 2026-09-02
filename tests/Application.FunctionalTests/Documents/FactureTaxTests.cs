using Microsoft.Extensions.DependencyInjection;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Facture.Commands.GenerateFactureCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Documents;

using static Testing;

/// <summary>
/// The tax breakdown an invoice has to carry to be an invoice. An issued one
/// keeps its own copy of the rate and the agency's tax number, because a rate is
/// changed by law and a reprint years later must still say what was charged.
/// </summary>
public class FactureTaxTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 5, 4, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ShouldFreezeTheBreakdownOnTheInvoice()
    {
        var agencyId = await SetUpAgencyAsync(vatRate: 19m, stamp: 1m, taxId: "1234567/A/M/000");
        var rentingId = await SeedRentingAsync(dailyRate: 120m);

        var dto = await SendAsync(new GenerateFactureCommand { RentingId = rentingId });

        // 3 billed days × 120 = 360 TTC.
        var facture = await FindAsync<Facture>(dto.Id);

        facture!.AgencyId.Should().Be(agencyId);
        facture.TotalAmount!.Amount.Should().Be(360m);
        facture.VatRatePercent.Should().Be(19m);
        facture.NetAmount!.Amount.Should().Be(302.52m);
        facture.VatAmount!.Amount.Should().Be(57.48m);
        facture.FiscalStampAmount!.Amount.Should().Be(1m);
        facture.TotalDue!.Amount.Should().Be(361m);
        facture.TaxIdentifier.Should().Be("1234567/A/M/000");
    }

    /// <summary>
    /// The property an accountant checks first: the two halves add up to the
    /// printed total exactly.
    /// </summary>
    [Test]
    public async Task NetPlusVat_ShouldEqualTheLineTotal()
    {
        await SetUpAgencyAsync(vatRate: 19m, stamp: 1m);
        var rentingId = await SeedRentingAsync(dailyRate: 33.33m);

        var dto = await SendAsync(new GenerateFactureCommand { RentingId = rentingId });
        var facture = await FindAsync<Facture>(dto.Id);

        (facture!.NetAmount!.Amount + facture.VatAmount!.Amount)
            .Should().Be(facture.TotalAmount!.Amount);
    }

    [Test]
    public async Task ShouldSurfaceTheBreakdownOnTheDto()
    {
        await SetUpAgencyAsync(vatRate: 19m, stamp: 1m, taxId: "9876543/B/C/000");
        var rentingId = await SeedRentingAsync(dailyRate: 120m);

        var dto = await SendAsync(new GenerateFactureCommand { RentingId = rentingId });

        dto.NetAmount!.Amount.Should().Be(302.52m);
        dto.VatAmount!.Amount.Should().Be(57.48m);
        dto.VatRatePercent.Should().Be(19m);
        dto.FiscalStampAmount!.Amount.Should().Be(1m);
        dto.TotalDue!.Amount.Should().Be(361m);
        dto.TaxIdentifier.Should().Be("9876543/B/C/000");
    }

    [Test]
    public async Task AnExemptAgency_ShouldInvoiceNetEqualToGross()
    {
        await SetUpAgencyAsync(vatRate: 0m, stamp: 0m);
        var rentingId = await SeedRentingAsync(dailyRate: 120m);

        var dto = await SendAsync(new GenerateFactureCommand { RentingId = rentingId });
        var facture = await FindAsync<Facture>(dto.Id);

        facture!.NetAmount!.Amount.Should().Be(360m);
        facture.VatAmount!.Amount.Should().Be(0m);
        facture.TotalDue!.Amount.Should().Be(360m);
    }

    /// <summary>
    /// Changing the agency's rate must not move an invoice already issued: the
    /// stored breakdown is what the client holds on paper.
    /// </summary>
    [Test]
    public async Task ChangingTheRateLater_ShouldNotMoveAnIssuedInvoice()
    {
        var agencyId = await SetUpAgencyAsync(vatRate: 19m, stamp: 1m);
        var rentingId = await SeedRentingAsync(dailyRate: 120m);

        var first = await SendAsync(new GenerateFactureCommand { RentingId = rentingId });

        await MutateSettingsAsync(agencyId, s => s.VatRatePercent = 7m);

        var second = await SendAsync(new GenerateFactureCommand { RentingId = rentingId });

        (await FindAsync<Facture>(first.Id))!.VatRatePercent.Should().Be(19m);
        (await FindAsync<Facture>(second.Id))!.VatRatePercent.Should().Be(7m);
    }

    private static async Task<int> SetUpAgencyAsync(
        decimal vatRate, decimal stamp, string? taxId = null)
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        await MutateSettingsAsync(agencyId, s =>
        {
            s.VatRatePercent = vatRate;
            s.FiscalStampAmount = stamp;
            s.TaxIdentifier = taxId;
        });

        return agencyId;
    }

    private static async Task MutateSettingsAsync(int agencyId, Action<AgencySettings> mutate)
    {
        var settings = (await AllAsync<AgencySettings>()).Single(s => s.AgencyId == agencyId);

        mutate(settings);
        await UpdateAsync(settings);

        // The provider caches each agency's snapshot, so a test that changes a
        // setting has to drop it or the next read still sees the old figures.
        await UsingScopeAsync(async provider =>
        {
            provider.GetRequiredService<IAgencySettingsProvider>().Invalidate(agencyId);
            await Task.CompletedTask;
            return 0;
        });
    }

    private static async Task<int> SeedRentingAsync(decimal dailyRate)
    {
        var car = new Car
        {
            Matricule = "TAX-1", Status = CarStatus.Active, DailyRate = Money.Of(dailyRate, "TND")
        };
        await AddAsync(car);

        var client = new Client
        {
            FirstName = "Tax", LastName = "Client", BirthDate = new DateTime(1990, 1, 1)
        };
        await AddAsync(client);

        var renting = RentingFixture.Hire(
            car.Id, client.Id, Start, End,
            // Three billed days at the daily rate, tax-inclusive.
            price: Money.Of(dailyRate * 3m, "TND"));

        await AddAsync(renting);
        return renting.Id;
    }
}
