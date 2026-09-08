namespace RemSolution.Application.Features.ExchangeRate.DTOs
{
    /// <summary>
    /// One quoted pair: <c>1 {FromCurrency} = {Rate} {ToCurrency}</c>. Read by
    /// the platform's rates screen and, anonymously, by the marketplace — which
    /// is why nothing agency-specific appears here.
    /// </summary>
    public class ExchangeRateDto
    {
        public int Id { get; init; }
        public string? FromCurrency { get; init; }
        public string? ToCurrency { get; init; }
        public decimal Rate { get; init; }

        /// <summary>
        /// The day the rate is quoted for. A wall-clock date, so it renders as
        /// the same day everywhere (`| date:'…':'UTC'`).
        /// </summary>
        public DateTime AsOf { get; init; }
    }
}
