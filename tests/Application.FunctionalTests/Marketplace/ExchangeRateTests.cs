using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.ExchangeRate.Commands.DeleteExchangeRateCommand;
using RemSolution.Application.Features.ExchangeRate.Commands.SetExchangeRateCommand;
using RemSolution.Application.Features.ExchangeRate.Queries.GetDisplayRatesQuery;
using RemSolution.Application.Features.ExchangeRate.Queries.GetExchangeRatesQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Marketplace;

using static Testing;

/// <summary>
/// The platform's display rates: who may quote them, and what the marketplace
/// gets back. The expansion into both directions is the only real rule in the
/// whole conversion, which is why it is tested here rather than left to the
/// browser that applies it.
/// </summary>
public class ExchangeRateTests : BaseTestFixture
{
    private static readonly DateTime AsOf = new(2030, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task PlatformAdministratorCanQuoteAPair()
    {
        await RunAsPlatformAdministratorAsync();

        var id = await SendAsync(new SetExchangeRateCommand
        {
            FromCurrency = "tnd", ToCurrency = "eur", Rate = 0.294118m, AsOf = AsOf
        });

        var rate = await FindAsync<ExchangeRate>(id);

        rate!.FromCurrency.Should().Be("TND");
        rate.ToCurrency.Should().Be("EUR");
        rate.Rate.Should().Be(0.294118m);
        rate.AsOf.Should().Be(AsOf);
    }

    [Test]
    public async Task QuotingTheSamePairAgainReplacesTheRate()
    {
        await RunAsPlatformAdministratorAsync();

        var first = await SendAsync(new SetExchangeRateCommand
        {
            FromCurrency = "TND", ToCurrency = "EUR", Rate = 0.29m, AsOf = AsOf
        });

        var second = await SendAsync(new SetExchangeRateCommand
        {
            FromCurrency = "TND", ToCurrency = "EUR", Rate = 0.31m, AsOf = AsOf.AddDays(30)
        });

        // The pair is the identity: a re-quote is the same row, not a second one.
        second.Should().Be(first);

        var rates = await SendAsync(new GetExchangeRatesQuery());
        rates.Should().ContainSingle();
        rates[0].Rate.Should().Be(0.31m);
    }

    [Test]
    public async Task ARateIsQuotedForADayEvenWhenNoneIsGiven()
    {
        await RunAsPlatformAdministratorAsync();

        var id = await SendAsync(new SetExchangeRateCommand
        {
            FromCurrency = "TND", ToCurrency = "EUR", Rate = 0.3m
        });

        var rate = await FindAsync<ExchangeRate>(id);

        // A converted price is only as honest as its date, so there is never a
        // rate without one.
        rate!.AsOf.Should().NotBe(default);
        rate.AsOf.Should().Be(rate.AsOf.Date);
    }

    [Test]
    public async Task ACurrencyCannotBeQuotedAgainstItself()
    {
        await RunAsPlatformAdministratorAsync();

        await FluentActions.Invoking(() => SendAsync(new SetExchangeRateCommand
        {
            FromCurrency = "EUR", ToCurrency = "eur", Rate = 1m
        })).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task TheReciprocalPairCannotBeQuotedAsWell()
    {
        await RunAsPlatformAdministratorAsync();

        await SendAsync(new SetExchangeRateCommand
        {
            FromCurrency = "TND", ToCurrency = "EUR", Rate = 0.29m
        });

        // The reverse direction is DERIVED from that row. Storing it too would
        // give every reader two answers for the same conversion — the shop
        // window expands both quotes, the invoice takes whichever comes back
        // first — and the ordered-pair unique index cannot say so.
        await FluentActions.Invoking(() => SendAsync(new SetExchangeRateCommand
        {
            FromCurrency = "EUR", ToCurrency = "TND", Rate = 3.5m
        })).Should().ThrowAsync<ValidationException>();

        (await SendAsync(new GetExchangeRatesQuery())).Should().ContainSingle();
    }

    [Test]
    public async Task TheRatesScreenSaysWhetherAFigureIsMaintainedOrDecided()
    {
        await RunAsPlatformAdministratorAsync();

        await SendAsync(new SetExchangeRateCommand
        {
            FromCurrency = "TND", ToCurrency = "EUR", Rate = 0.29m, IsPinned = true
        });

        var rates = await SendAsync(new GetExchangeRatesQuery());

        // Both columns reach the screen, or the edit form prefills a pinned pair
        // as unpinned and saving it hands the pair back to the refresh.
        rates.Should().ContainSingle();
        rates[0].IsPinned.Should().BeTrue();
        rates[0].RefreshedAt.Should().BeNull("a person quoted it, not the feed");
    }

    [Test]
    public async Task ARateMustBePositive()
    {
        await RunAsPlatformAdministratorAsync();

        await FluentActions.Invoking(() => SendAsync(new SetExchangeRateCommand
        {
            FromCurrency = "TND", ToCurrency = "EUR", Rate = 0m
        })).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task OnlyThePlatformQuotesRates()
    {
        await RunAsAgencyAdministratorAsync();

        await FluentActions.Invoking(() => SendAsync(new SetExchangeRateCommand
        {
            FromCurrency = "TND", ToCurrency = "EUR", Rate = 0.3m
        })).Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task WithdrawingARateRemovesIt()
    {
        await RunAsPlatformAdministratorAsync();

        var id = await SendAsync(new SetExchangeRateCommand
        {
            FromCurrency = "TND", ToCurrency = "EUR", Rate = 0.3m
        });

        await SendAsync(new DeleteExchangeRateCommand(id));

        (await SendAsync(new GetExchangeRatesQuery())).Should().BeEmpty();
    }

    // ------------------------------------------------- what the shop window gets ---

    [Test]
    public async Task TheMarketplaceGetsBothDirectionsOfEveryQuote()
    {
        await RunAsPlatformAdministratorAsync();
        await SendAsync(new SetExchangeRateCommand
        {
            FromCurrency = "TND", ToCurrency = "EUR", Rate = 0.294118m, AsOf = AsOf
        });

        var rates = await SendAsync(new GetDisplayRatesQuery());

        rates.Should().HaveCount(2);

        var forward = rates.Single(r => r.From == "TND" && r.To == "EUR");
        forward.Rate.Should().Be(0.294118m);
        forward.AsOf.Should().Be(AsOf);

        // The reciprocal is derived, never stored — two rows that must agree are
        // two rows that eventually will not.
        var back = rates.Single(r => r.From == "EUR" && r.To == "TND");
        back.Rate.Should().Be(3.399996m); // 1 / 0.294118, to six places
        back.AsOf.Should().Be(AsOf);
    }

    [Test]
    public async Task TheMarketplaceDoesNotChainRatesTogether()
    {
        await RunAsPlatformAdministratorAsync();
        await SendAsync(new SetExchangeRateCommand { FromCurrency = "TND", ToCurrency = "EUR", Rate = 0.3m });
        await SendAsync(new SetExchangeRateCommand { FromCurrency = "EUR", ToCurrency = "MAD", Rate = 10.5m });

        var rates = await SendAsync(new GetDisplayRatesQuery());

        // Four rows, not six: TND → MAD would compound two roundings into a
        // figure a visitor could read as a price, so it is not offered until the
        // platform quotes that pair itself.
        rates.Should().HaveCount(4);
        rates.Should().NotContain(r => r.From == "TND" && r.To == "MAD");
    }

    [Test]
    public async Task AVisitorReadsTheRatesWithoutAnAccount()
    {
        await RunAsPlatformAdministratorAsync();
        await SendAsync(new SetExchangeRateCommand { FromCurrency = "TND", ToCurrency = "EUR", Rate = 0.3m });

        // The shop window is anonymous, and most browsing happens signed out.
        SetCurrentUser(null);
        SetCurrentAgency(null);

        (await SendAsync(new GetDisplayRatesQuery())).Should().HaveCount(2);
    }

    [Test]
    public async Task WithNothingQuotedTheMarketplaceOffersNoConversion()
    {
        SetCurrentUser(null);
        SetCurrentAgency(null);

        // Not an error: every price is then shown in its agency's own currency,
        // which is what it is charged in anyway.
        (await SendAsync(new GetDisplayRatesQuery())).Should().BeEmpty();
    }
}
