namespace RemSolution.Domain.ValueObjects
{
    /// <summary>
    /// How many decimal places a currency actually has — its ISO 4217 minor unit.
    /// <para>
    /// Needed because two is not the universal answer, and this product's home
    /// currency is one of the exceptions: the Tunisian dinar divides into 1000
    /// millimes, not 100 centimes. Printing "1 250,00 TND" understates the unit,
    /// and printing "1 250,00 JPY" invents one the yen does not have.
    /// </para>
    /// <para>
    /// This governs how a figure is ROUNDED AND PRINTED, not how one is stored:
    /// every <c>Money</c> column is <c>decimal(18,2)</c> and stays that way (see
    /// <see cref="TaxBreakdown"/>). Widening the schema is a separate decision
    /// with a data migration behind it; this is only about not printing a wrong
    /// number of zeroes on a converted figure.
    /// </para>
    /// </summary>
    public static class CurrencyDecimals
    {
        /// <summary>What almost every currency uses.</summary>
        public const int Default = 2;

        // ISO 4217 exponent 0 — no minor unit at all.
        private static readonly HashSet<string> NoMinorUnit = new(StringComparer.OrdinalIgnoreCase)
        {
            "BIF", "CLP", "DJF", "GNF", "ISK", "JPY", "KMF", "KRW",
            "PYG", "RWF", "UGX", "UYI", "VND", "VUV", "XAF", "XOF", "XPF"
        };

        // ISO 4217 exponent 3 — thousandths. TND is here, which is the whole
        // reason this type exists.
        private static readonly HashSet<string> Thousandths = new(StringComparer.OrdinalIgnoreCase)
        {
            "BHD", "IQD", "JOD", "KWD", "LYD", "OMR", "TND"
        };

        /// <summary>
        /// The minor-unit digits for an ISO 4217 code, and <see cref="Default"/>
        /// for anything unrecognised — an unknown code is far more likely to be a
        /// two-decimal currency than a typo worth failing a document over.
        /// </summary>
        public static int For(string? currency)
        {
            if (string.IsNullOrWhiteSpace(currency)) return Default;

            var code = currency.Trim();

            if (NoMinorUnit.Contains(code)) return 0;
            if (Thousandths.Contains(code)) return 3;

            return Default;
        }
    }
}
