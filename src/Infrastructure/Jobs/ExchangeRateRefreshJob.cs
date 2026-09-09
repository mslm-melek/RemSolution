using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Infrastructure.Jobs;

/// <summary>
/// Keeps the quoted pairs' values current from the automatic feed.
/// <para>
/// It REFRESHES pairs, it never creates them. Which currencies the marketplace
/// offers is a product decision — the SPA's picker derives its options from the
/// pairs that exist — so the platform administrator still decides what is quoted
/// and this only decides what it is worth today.
/// </para>
/// <para>
/// Platform-level data, so unlike the other sweeps there is no tenant to push:
/// <c>ExchangeRate</c> is not an <c>ITenantEntity</c> and no agency owns a rate.
/// </para>
/// </summary>
public sealed class ExchangeRateRefreshJob
{
    private readonly IApplicationDbContext _context;
    private readonly IExchangeRateProvider _provider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ExchangeRateRefreshJob> _logger;

    public ExchangeRateRefreshJob(
        IApplicationDbContext context,
        IExchangeRateProvider provider,
        TimeProvider timeProvider,
        ILogger<ExchangeRateRefreshJob> logger)
    {
        _context = context;
        _provider = provider;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task RunAsync()
    {
        var now = _timeProvider.GetUtcNow();

        var due = await _context.ExchangeRates
            .Where(r => !r.IsPinned)
            .ToListAsync();

        if (due.Count == 0)
        {
            return;
        }

        var refreshed = 0;

        // One call per distinct base rather than per pair: the feed answers with
        // every rate against a base, so TND→EUR and TND→MAD are one request.
        foreach (var group in due.GroupBy(r => r.FromCurrency))
        {
            var quote = await _provider.GetAsync(group.Key, CancellationToken.None);

            if (quote is null)
            {
                // Yesterday's rate stands, and it still carries its own AsOf, so
                // the screen shows how old it is rather than pretending.
                _logger.LogInformation(
                    "No rate available for base {Base}; keeping the {Count} stored rate(s)",
                    group.Key, group.Count());
                continue;
            }

            foreach (var rate in group)
            {
                if (!quote.Rates.TryGetValue(rate.ToCurrency, out var candidate))
                {
                    _logger.LogWarning(
                        "Feed quoted {Base} but not {Target}; keeping the stored rate",
                        group.Key, rate.ToCurrency);
                    continue;
                }

                if (!ExchangeRateRefresh.IsPlausible(rate.Rate, candidate))
                {
                    // Loud, because it is either a provider gone wrong or a real
                    // devaluation somebody has to look at — and either way the
                    // marketplace keeps showing the last figure a human saw.
                    _logger.LogError(
                        "Refused an implausible rate for {Base}->{Target}: {Current} to {Candidate}",
                        group.Key, rate.ToCurrency, rate.Rate, candidate);
                    continue;
                }

                rate.Refresh(candidate, quote.AsOf, now);
                refreshed++;
            }
        }

        if (refreshed > 0)
        {
            await _context.SaveChangesAsync(CancellationToken.None);
            _logger.LogInformation("Refreshed {Count} exchange rate(s)", refreshed);
        }
    }
}
