namespace RemSolution.Domain.ValueObjects
{
    /// <summary>
    /// Whether a rate coming back from a provider is worth believing.
    /// <para>
    /// The one place this arithmetic lives, like <see cref="CancellationPolicy"/>:
    /// a refreshed rate lands unattended on the public marketplace, so the check
    /// that stops an absurd one has to be somewhere a test can reach it rather
    /// than inline in a background job.
    /// </para>
    /// </summary>
    public static class ExchangeRateRefresh
    {
        /// <summary>
        /// How far a rate may move in one refresh before it is refused. A real
        /// currency does not move 25% overnight — a provider changing its base,
        /// its scale, or answering with a different currency's figure does. The
        /// bound is deliberately generous: refusing a genuine devaluation costs a
        /// stale second line, while accepting a garbage rate prices a whole
        /// marketplace wrong.
        /// </summary>
        public const decimal MaxChangePercent = 25m;

        /// <summary>
        /// Whether <paramref name="candidate"/> may replace <paramref name="current"/>.
        /// A non-positive candidate is never plausible (the entity refuses it too);
        /// a non-positive current means there is nothing to compare against, so
        /// anything positive is accepted.
        /// </summary>
        public static bool IsPlausible(decimal current, decimal candidate)
        {
            if (candidate <= 0m) return false;
            if (current <= 0m) return true;

            var change = Math.Abs(candidate - current) / current * 100m;

            return change <= MaxChangePercent;
        }
    }
}
