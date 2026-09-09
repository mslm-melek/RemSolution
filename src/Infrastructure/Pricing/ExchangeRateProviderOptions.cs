namespace RemSolution.Infrastructure.Pricing;

/// <summary>
/// The automatic rate refresh, as configured. Defaults are the working ones, so
/// a fresh checkout needs no configuration; a deployment that would rather not
/// call out to the internet sets <c>ExchangeRates:Enabled</c> to false.
/// </summary>
public class ExchangeRateProviderOptions
{
    public const string SectionName = "ExchangeRates";

    /// <summary>
    /// Whether the daily refresh runs at all. The job is also only ever
    /// registered where Hangfire has real storage, so tests and the NSwag
    /// build-time host never reach the network regardless of this.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Base address, trailing slash included — the provider appends the base
    /// currency code to it.
    /// </summary>
    public string BaseUrl { get; set; } = "https://open.er-api.com/v6/latest/";

    /// <summary>
    /// How long to wait for the feed. Short on purpose: this is a background
    /// courtesy, and a request that has not answered in ten seconds should leave
    /// yesterday's rate in place rather than hold the job.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
}
