using Microsoft.Extensions.DependencyInjection;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Facture.Commands.GenerateFactureCommand;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Documents;

using static Testing;

/// <summary>
/// The courtesy second currency on an invoice. The figure itself is only a
/// multiplication (covered in the domain tests); what is tested here is the part
/// that would go wrong in production — that the RATE is frozen at issue, that a
/// missing rate costs one line rather than the invoice, and that an agency which
/// asked for nothing gets nothing.
/// </summary>
public class FactureDisplayCurrencyTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 5, 4, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime RateAsOf = new(2030, 4, 28, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ShouldFreezeTheRateAndItsDateOnTheInvoice()
    {
        var agencyId = await SetUpAgencyAsync(displayCurrency: "EUR");
        await QuoteAsync("TND", "EUR", 0.296247m);

        var rentingId = await SeedRentingAsync(dailyRate: 120m);
        var dto = await SendAsync(new GenerateFactureCommand { RentingId = rentingId });

        var facture = await FindAsync<Facture>(dto.Id);

        facture!.DisplayCurrency.Should().Be("EUR");
        facture.DisplayExchangeRate.Should().Be(
            0.296247m, "an 18,2 column would have rounded this to 0.30");
        facture.DisplayRateAsOf.Should().Be(RateAsOf);

        // The converted amount is deliberately NOT stored — it is derived from
        // the frozen rate, which is one fewer figure that can disagree.
        DisplayConversion.Apply(facture.TotalDue!, facture.DisplayExchangeRate!.Value, "EUR")
            .Amount.Should().Be(106.95m, "361 TND at 0.296247 is 106.945167, rounded away from zero");

        agencyId.Should().BePositive();
    }

    /// <summary>
    /// The reason the rate is frozen at all: rates move every night, so a
    /// reprint that looked one up again would show a different figure each time
    /// the document was opened.
    /// </summary>
    [Test]
    public async Task ChangingTheRateLater_ShouldNotMoveAnIssuedInvoice()
    {
        await SetUpAgencyAsync(displayCurrency: "EUR");
        await QuoteAsync("TND", "EUR", 0.290m);

        var rentingId = await SeedRentingAsync(dailyRate: 120m);
        var first = await SendAsync(new GenerateFactureCommand { RentingId = rentingId });

        await QuoteAsync("TND", "EUR", 0.310m);

        var second = await SendAsync(new GenerateFactureCommand { RentingId = rentingId });

        (await FindAsync<Facture>(first.Id))!.DisplayExchangeRate.Should().Be(0.290m);
        (await FindAsync<Facture>(second.Id))!.DisplayExchangeRate.Should().Be(0.310m);
    }

    /// <summary>
    /// Only one row per ordered pair is stored and the reciprocal is derived, so
    /// an agency whose platform quoted EUR → TND must still get its TND → EUR
    /// figure — the same inversion the marketplace does.
    /// </summary>
    [Test]
    public async Task ShouldInvertAQuoteHeldInTheOtherDirection()
    {
        await SetUpAgencyAsync(displayCurrency: "EUR");
        await QuoteAsync("EUR", "TND", 3.375m);

        var rentingId = await SeedRentingAsync(dailyRate: 120m);
        var dto = await SendAsync(new GenerateFactureCommand { RentingId = rentingId });

        var facture = await FindAsync<Facture>(dto.Id);

        facture!.DisplayCurrency.Should().Be("EUR");
        facture.DisplayExchangeRate.Should().BeApproximately(1m / 3.375m, 0.000001m);
    }

    [Test]
    public async Task WithNoRateQuoted_ShouldStillIssueTheInvoiceAndFreezeNothing()
    {
        // The pair is simply not quoted. An invoice must never fail to issue
        // because a display nicety is unavailable.
        await SetUpAgencyAsync(displayCurrency: "EUR");

        var rentingId = await SeedRentingAsync(dailyRate: 120m);
        var dto = await SendAsync(new GenerateFactureCommand { RentingId = rentingId });

        var facture = await FindAsync<Facture>(dto.Id);

        facture!.TotalDue!.Amount.Should().Be(361m, "the invoice is unaffected");
        facture.DisplayCurrency.Should().BeNull();
        facture.DisplayExchangeRate.Should().BeNull();
        facture.DisplayRateAsOf.Should().BeNull();
    }

    [Test]
    public async Task WithNoSecondCurrencyAsked_ShouldFreezeNothing()
    {
        await SetUpAgencyAsync(displayCurrency: null);
        await QuoteAsync("TND", "EUR", 0.296247m);

        var rentingId = await SeedRentingAsync(dailyRate: 120m);
        var dto = await SendAsync(new GenerateFactureCommand { RentingId = rentingId });

        (await FindAsync<Facture>(dto.Id))!.DisplayCurrency
            .Should().BeNull("a quoted rate is not a reason to print one");
    }

    [Test]
    public async Task AskingForTheAgencysOwnCurrency_ShouldFreezeNothing()
    {
        // "Also show it in dinars" on an agency billing in dinars is a line
        // saying 1 = 1.
        await SetUpAgencyAsync(displayCurrency: "TND");

        var rentingId = await SeedRentingAsync(dailyRate: 120m);
        var dto = await SendAsync(new GenerateFactureCommand { RentingId = rentingId });

        (await FindAsync<Facture>(dto.Id))!.DisplayCurrency.Should().BeNull();
    }

    // --- setup ----------------------------------------------------------------

    private static async Task<int> SetUpAgencyAsync(string? displayCurrency)
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        var settings = (await AllAsync<AgencySettings>()).Single(s => s.AgencyId == agencyId);
        settings.VatRatePercent = 19m;
        settings.FiscalStampAmount = 1m;
        settings.InvoiceDisplayCurrency = displayCurrency;
        await UpdateAsync(settings);

        // The provider caches each agency's snapshot, so a changed setting has to
        // drop it or the next read still sees the old one.
        await UsingScopeAsync(async provider =>
        {
            provider.GetRequiredService<IAgencySettingsProvider>().Invalidate(agencyId);
            await Task.CompletedTask;
            return 0;
        });

        return agencyId;
    }

    /// <summary>
    /// Writes the row directly rather than through SetExchangeRateCommand. The
    /// command is the platform administrator's, and switching identities
    /// mid-test — twice, for the re-quote case — would fight the fixture for no
    /// gain: what these tests are about is the invoice, and the rate is a
    /// precondition. The command's own behaviour is covered in
    /// ExchangeRateTests.
    /// </summary>
    private static async Task QuoteAsync(string from, string to, decimal rate)
    {
        var existing = (await AllAsync<ExchangeRate>())
            .FirstOrDefault(r => r.FromCurrency == from && r.ToCurrency == to);

        if (existing is null)
        {
            await AddAsync(ExchangeRate.Create(from, to, rate, RateAsOf));
            return;
        }

        existing.Amend(rate, RateAsOf);
        await UpdateAsync(existing);
    }

    private static async Task<int> SeedRentingAsync(decimal dailyRate)
    {
        var car = new Car
        {
            Matricule = "CUR-1", Status = CarStatus.Active, DailyRate = Money.Of(dailyRate, "TND")
        };
        await AddAsync(car);

        var client = new Client
        {
            FirstName = "Currency", LastName = "Client", BirthDate = new DateTime(1990, 1, 1)
        };
        await AddAsync(client);

        var renting = RentingFixture.Hire(
            car.Id, client.Id, Start, End, price: Money.Of(dailyRate * 3m, "TND"));

        await AddAsync(renting);
        return renting.Id;
    }
}
