using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.ExchangeRate.Commands.SetExchangeRateCommand;
using RemSolution.Domain.Entities;
using RemSolution.Infrastructure.Jobs;

namespace RemSolution.Application.FunctionalTests.Marketplace;

using static Testing;

/// <summary>
/// The nightly refresh of the display rates. Every rule here is about what
/// happens when the feed is wrong, absent, or answering about something else —
/// the happy path is one line, and the rest is why this job is safe to leave
/// running unattended against a public marketplace.
/// </summary>
public class ExchangeRateRefreshJobTests : BaseTestFixture
{
    private static readonly DateTime Yesterday = new(2030, 3, 31, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Today = new(2030, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// A provider that answers from a script, and counts what it was asked — the
    /// job promises one call per distinct base, not one per pair.
    /// </summary>
    private sealed class ScriptedProvider : IExchangeRateProvider
    {
        private readonly Dictionary<string, ExchangeRateQuote?> _answers;

        public ScriptedProvider(Dictionary<string, ExchangeRateQuote?> answers) => _answers = answers;

        public List<string> Asked { get; } = new();

        public Task<ExchangeRateQuote?> GetAsync(string baseCurrency, CancellationToken cancellationToken)
        {
            Asked.Add(baseCurrency);
            return Task.FromResult(_answers.TryGetValue(baseCurrency, out var quote) ? quote : null);
        }
    }

    private static ExchangeRateQuote Quote(string @base, params (string To, decimal Rate)[] rates) =>
        new(@base, Today, rates.ToDictionary(r => r.To, r => r.Rate));

    private static Task RunJobAsync(ScriptedProvider provider) =>
        UsingScopeAsync(async sp =>
        {
            // Constructed by hand rather than resolved: the registered provider is
            // the real HTTP one, and a test must never reach the internet.
            var job = new ExchangeRateRefreshJob(
                sp.GetRequiredService<IApplicationDbContext>(),
                provider,
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILogger<ExchangeRateRefreshJob>>());

            await job.RunAsync();
            return true;
        });

    // Signing the platform administrator in is once per test, not once per quote:
    // RunAsPlatformAdministratorAsync creates the account and throws on a second
    // call within the same test.
    private static Task<int> QuoteAsync(
        string from, string to, decimal rate, bool pinned = false) =>
        SendAsync(new SetExchangeRateCommand
        {
            FromCurrency = from, ToCurrency = to, Rate = rate, AsOf = Yesterday, IsPinned = pinned
        });

    private static Task<ExchangeRate?> RateAsync(int id) => FindAsync<ExchangeRate>(id);

    [Test]
    public async Task RefreshesAQuotedPairAndRecordsThatItWasAutomatic()
    {
        await RunAsPlatformAdministratorAsync();

        var id = await QuoteAsync("TND", "EUR", 0.290m);

        var provider = new ScriptedProvider(new() { ["TND"] = Quote("TND", ("EUR", 0.296m)) });
        await RunJobAsync(provider);

        var rate = await RateAsync(id);

        rate!.Rate.Should().Be(0.296m);
        rate.AsOf.Should().Be(Today);
        rate.RefreshedAt.Should().NotBeNull("the screen has to be able to say this is maintained");
        rate.IsPinned.Should().BeFalse();
    }

    [Test]
    public async Task AsksOncePerBaseAndNotOncePerPair()
    {
        await RunAsPlatformAdministratorAsync();

        var eur = await QuoteAsync("TND", "EUR", 0.290m);
        var mad = await QuoteAsync("TND", "MAD", 3.20m);

        var provider = new ScriptedProvider(new()
        {
            ["TND"] = Quote("TND", ("EUR", 0.296m), ("MAD", 3.23m))
        });

        await RunJobAsync(provider);

        provider.Asked.Should().Equal("TND");
        (await RateAsync(eur))!.Rate.Should().Be(0.296m);
        (await RateAsync(mad))!.Rate.Should().Be(3.23m);
    }

    [Test]
    public async Task LeavesAPinnedPairAloneAndDoesNotEvenAskForIt()
    {
        await RunAsPlatformAdministratorAsync();

        var pinned = await QuoteAsync("TND", "EUR", 0.290m, pinned: true);

        var provider = new ScriptedProvider(new() { ["TND"] = Quote("TND", ("EUR", 0.296m)) });
        await RunJobAsync(provider);

        (await RateAsync(pinned))!.Rate.Should().Be(0.290m, "the administrator arbitrated this one");
        provider.Asked.Should().BeEmpty("a base nothing needs is a call not worth making");
    }

    [Test]
    public async Task KeepsTheStoredRateWhenTheFeedSaysNothing()
    {
        await RunAsPlatformAdministratorAsync();

        var id = await QuoteAsync("TND", "EUR", 0.290m);

        // The provider is down, rate-limited, or answering nonsense.
        var provider = new ScriptedProvider(new() { ["TND"] = null });
        await RunJobAsync(provider);

        var rate = await RateAsync(id);

        rate!.Rate.Should().Be(0.290m);
        rate.AsOf.Should().Be(Yesterday, "the stale date is what tells the visitor it is stale");
        rate.RefreshedAt.Should().BeNull();
    }

    [Test]
    public async Task RefusesAnImplausibleRateRatherThanPricingTheMarketplaceWrong()
    {
        await RunAsPlatformAdministratorAsync();

        var id = await QuoteAsync("TND", "EUR", 0.290m);

        // 0.29 to 3.23 is the MAD figure, not a devaluation — the shape of
        // failure a feed changing its base produces.
        var provider = new ScriptedProvider(new() { ["TND"] = Quote("TND", ("EUR", 3.23m)) });
        await RunJobAsync(provider);

        (await RateAsync(id))!.Rate.Should().Be(0.290m);
    }

    [Test]
    public async Task KeepsTheStoredRateWhenTheFeedDoesNotQuoteThatCurrency()
    {
        await RunAsPlatformAdministratorAsync();

        var id = await QuoteAsync("TND", "XOF", 180m);

        var provider = new ScriptedProvider(new() { ["TND"] = Quote("TND", ("EUR", 0.296m)) });
        await RunJobAsync(provider);

        (await RateAsync(id))!.Rate.Should().Be(180m);
    }

    [Test]
    public async Task NeverCreatesAPairNobodyQuoted()
    {
        await RunAsPlatformAdministratorAsync();
        await QuoteAsync("TND", "EUR", 0.290m);

        // The feed offers every currency it knows; which ones the marketplace
        // OFFERS stays the platform administrator's decision.
        var provider = new ScriptedProvider(new()
        {
            ["TND"] = Quote("TND", ("EUR", 0.296m), ("MAD", 3.23m), ("AED", 1.26m))
        });

        await RunJobAsync(provider);

        (await AllAsync<ExchangeRate>()).Should().HaveCount(1);
    }

    [Test]
    public async Task AManualRequoteTakesThePairBackFromTheFeed()
    {
        await RunAsPlatformAdministratorAsync();

        var id = await QuoteAsync("TND", "EUR", 0.290m);

        await RunJobAsync(new ScriptedProvider(new() { ["TND"] = Quote("TND", ("EUR", 0.296m)) }));
        (await RateAsync(id))!.RefreshedAt.Should().NotBeNull();

        await SendAsync(new SetExchangeRateCommand
        {
            FromCurrency = "TND", ToCurrency = "EUR", Rate = 0.300m, AsOf = Today
        });

        var rate = await RateAsync(id);
        rate!.Rate.Should().Be(0.300m);
        rate.RefreshedAt.Should().BeNull("this figure is a person's now, and the screen says so");
    }

    [Test]
    public async Task DoesNothingWhenNoPairIsQuoted()
    {
        await RunAsPlatformAdministratorAsync();

        var provider = new ScriptedProvider(new() { ["TND"] = Quote("TND", ("EUR", 0.296m)) });
        await RunJobAsync(provider);

        provider.Asked.Should().BeEmpty();
        (await AllAsync<ExchangeRate>()).Should().BeEmpty();
    }
}

