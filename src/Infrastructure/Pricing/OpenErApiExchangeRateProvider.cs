using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Infrastructure.Pricing;

/// <summary>
/// <see cref="IExchangeRateProvider"/> over open.er-api.com — the free, key-less
/// tier of exchangerate-api.com.
/// <para>
/// Chosen for one reason: it quotes TND, MAD and AED. The obvious alternative
/// (the ECB feed, via Frankfurter) is better-sourced but publishes none of them,
/// and this product's home currency is the dinar. The trade is accepted knowingly
/// — these are INDICATIVE market rates, not the Banque Centrale de Tunisie's
/// official ones, which is tolerable only because the conversion is display-only
/// and never touches a stored amount (see the ExchangeRate entity).
/// </para>
/// <para>
/// It accepts an arbitrary base, so a pair is read directly as
/// <c>base=From, rates[To]</c> — no cross-rate through USD, and therefore no
/// chain, which is the rule this feature had to fit.
/// </para>
/// </summary>
public sealed class OpenErApiExchangeRateProvider : IExchangeRateProvider
{
    public const string HttpClientName = "exchange-rates";

    private readonly HttpClient _http;
    private readonly TimeProvider _dateTime;
    private readonly ILogger<OpenErApiExchangeRateProvider> _logger;

    public OpenErApiExchangeRateProvider(
        HttpClient http,
        TimeProvider dateTime,
        ILogger<OpenErApiExchangeRateProvider> logger)
    {
        _http = http;
        _dateTime = dateTime;
        _logger = logger;
    }

    public async Task<ExchangeRateQuote?> GetAsync(
        string baseCurrency, CancellationToken cancellationToken)
    {
        var code = baseCurrency.Trim().ToUpperInvariant();

        try
        {
            // Relative to the configured base address, which carries the trailing
            // slash (see ExchangeRateProviderOptions).
            var payload = await _http.GetFromJsonAsync<OpenErApiResponse>(code, cancellationToken);

            if (payload is null || !string.Equals(payload.Result, "success", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Exchange-rate feed answered {Result} for base {Base}", payload?.Result, code);
                return null;
            }

            if (payload.Rates is not { Count: > 0 })
            {
                _logger.LogWarning("Exchange-rate feed returned no rates for base {Base}", code);
                return null;
            }

            // Trust the payload's own base over the one asked for: if the feed
            // answered about a different currency, every rate below would be
            // attributed to the wrong pair.
            if (!string.Equals(payload.BaseCode, code, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Exchange-rate feed answered for base {Answered} when asked for {Base}",
                    payload.BaseCode, code);
                return null;
            }

            return new ExchangeRateQuote(code, AsOf(payload.LastUpdateUnix), payload.Rates);
        }
        // NotSupportedException is the one that does not look like a network
        // failure: GetFromJsonAsync throws it when the body is not JSON at all,
        // which is what a proxy or a WAF block page answers with.
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                      or System.Text.Json.JsonException
                                      or NotSupportedException)
        {
            // The feed being unreachable, slow or malformed is an ordinary
            // Tuesday; the job keeps yesterday's rate. See the interface.
            _logger.LogWarning(ex, "Exchange-rate feed unreachable for base {Base}", code);
            return null;
        }
    }

    /// <summary>
    /// The day the feed quoted for. A wall-clock DATE, like every other
    /// <c>AsOf</c>: it is printed beside a converted price, not compared as an
    /// instant. Falls back to today when the feed omits the stamp.
    /// </summary>
    private DateTime AsOf(long? lastUpdateUnix) => lastUpdateUnix is long unix && unix > 0
        ? DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.Date
        : _dateTime.GetUtcNow().UtcDateTime.Date;

    // Only the four fields worth reading; the feed also returns its own
    // documentation and terms-of-use URLs.
    private sealed record OpenErApiResponse
    {
        [JsonPropertyName("result")]
        public string? Result { get; init; }

        [JsonPropertyName("base_code")]
        public string? BaseCode { get; init; }

        [JsonPropertyName("time_last_update_unix")]
        public long? LastUpdateUnix { get; init; }

        [JsonPropertyName("rates")]
        public Dictionary<string, decimal>? Rates { get; init; }
    }
}
