namespace RemSolution.Domain.ValueObjects
{
    /// <summary>
    /// Turns an amount into another currency for READING, and nowhere else.
    /// <para>
    /// The one place this multiplication lives, so the courtesy line on an
    /// invoice and anything that later wants the same figure round it the same
    /// way. What it produces is never stored and never owed: an invoice stays
    /// denominated in the agency's currency, and the converted figure beside the
    /// total is informational (see <c>Facture</c>'s conversion fields).
    /// </para>
    /// <para>
    /// ONLY THE TOTAL IS EVER CONVERTED, deliberately. Converting the net and the
    /// tax lines as well would either break <c>Net + Vat == Gross</c> in the
    /// target currency or need a second rounding discipline to hold it together —
    /// and it would suggest the tax was charged in that currency, which is false.
    /// One figure, clearly labelled, has nothing to make balance.
    /// </para>
    /// </summary>
    public static class DisplayConversion
    {
        /// <summary>
        /// <paramref name="amount"/> read in <paramref name="toCurrency"/> at
        /// <paramref name="rate"/>, where the rate is <c>1 amount.Currency =
        /// rate toCurrency</c>. Rounded to the TARGET currency's own minor unit,
        /// which is not always two places (see <see cref="CurrencyDecimals"/>).
        /// </summary>
        public static Money Apply(Money amount, decimal rate, string toCurrency)
        {
            ArgumentNullException.ThrowIfNull(amount);

            if (rate <= 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rate), rate, "A rate is greater than zero.");
            }

            var converted = Math.Round(
                amount.Amount * rate,
                CurrencyDecimals.For(toCurrency),
                MidpointRounding.AwayFromZero);

            return Money.Of(converted, toCurrency);
        }
    }
}
