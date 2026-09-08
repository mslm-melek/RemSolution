namespace RemSolution.Application.Features.ExchangeRate.DTOs
{
    /// <summary>
    /// A rate the marketplace can actually apply: <c>1 {From} = {Rate} {To}</c>,
    /// with both directions of every quoted pair already worked out.
    /// <para>
    /// Derived rather than stored — the platform quotes a pair once and the
    /// reverse is computed here (see <c>GetDisplayRatesQuery</c>). Expanding it
    /// on the server rather than in the browser is deliberate: which direction
    /// applies is the only real rule in the conversion, and this keeps it
    /// somewhere the test suite runs.
    /// </para>
    /// </summary>
    public class DisplayRateDto
    {
        public string? From { get; init; }
        public string? To { get; init; }
        public decimal Rate { get; init; }

        /// <summary>The day the underlying quote is for. Wall-clock, like its source.</summary>
        public DateTime AsOf { get; init; }
    }
}
