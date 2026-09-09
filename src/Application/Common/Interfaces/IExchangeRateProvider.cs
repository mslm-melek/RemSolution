namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// One base currency's rates as a provider answered them: <c>1 BaseCurrency =
/// Rates[X] X</c>, quoted for <see cref="AsOf"/>.
/// </summary>
public sealed record ExchangeRateQuote(
    string BaseCurrency,
    DateTime AsOf,
    IReadOnlyDictionary<string, decimal> Rates);

/// <summary>
/// Reads today's rates from an outside feed. The one seam between the refresh job
/// and the internet, so the job's rules — what is pinned, what is plausible, what
/// happens when the answer is nonsense — are testable without a network.
/// </summary>
public interface IExchangeRateProvider
{
    /// <summary>
    /// Rates against <paramref name="baseCurrency"/>, or <c>null</c> when the feed
    /// could not be read or did not answer usefully.
    /// <para>
    /// NULL RATHER THAN AN EXCEPTION, deliberately: a provider being down is an
    /// ordinary Tuesday, and the right response is to keep yesterday's rate
    /// rather than to fail a job and retry into the same silence. Implementations
    /// log the reason and return null.
    /// </para>
    /// </summary>
    Task<ExchangeRateQuote?> GetAsync(string baseCurrency, CancellationToken cancellationToken);
}
