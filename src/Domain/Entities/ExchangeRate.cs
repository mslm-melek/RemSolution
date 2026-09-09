using RemSolution.Domain.Exceptions;

namespace RemSolution.Domain.Entities
{
    /// <summary>
    /// What one currency is worth in another, kept by the platform:
    /// <c>1 {FromCurrency} = {Rate} {ToCurrency}</c>.
    /// <para>
    /// This is a **display** rate and nothing else. No stored amount is ever
    /// converted: an agency bills in one currency, and its invoices, payments,
    /// credits and statistics stay in it. The rate only lets a marketplace
    /// visitor read a price in a currency they think in, which is why nothing
    /// here is frozen onto a document the way a VAT rate is — there is no
    /// document to freeze it onto.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Platform-level, like <see cref="SubscriptionPlan"/>: rates belong to the
    /// marketplace as a whole, an anonymous visitor reads them, and no agency
    /// owns one. Not an <c>ITenantEntity</c>, so there is no query filter behind
    /// the reads.
    /// <para>
    /// One row per ordered pair. The other direction is NOT stored — it is
    /// derived when needed (see the SPA's <c>currency.ts</c>), because two rows
    /// that must be reciprocals are two rows that will eventually disagree.
    /// </para>
    /// </remarks>
    public class ExchangeRate : BaseAuditableEntity
    {
        public string FromCurrency { get; private set; } = string.Empty;
        public string ToCurrency { get; private set; } = string.Empty;

        /// <summary>How many <see cref="ToCurrency"/> one <see cref="FromCurrency"/> buys.</summary>
        public decimal Rate { get; private set; }

        /// <summary>
        /// The day the rate is quoted for — a date a person picked, not an
        /// instant, so it renders as that same day everywhere (see the UTC rule).
        /// Shown to the visitor: a converted price is only as honest as its date.
        /// </summary>
        public DateTime AsOf { get; private set; }

        /// <summary>
        /// When the automatic refresh last wrote this row, and null when a person
        /// last did — so one column answers "is this figure being maintained, or
        /// is it somebody's decision?". <see cref="Amend"/> clears it for exactly
        /// that reason.
        /// </summary>
        public DateTimeOffset? RefreshedAt { get; private set; }

        /// <summary>
        /// Keeps the daily refresh off this pair. The rate an administrator
        /// arbitrated by hand would otherwise be overwritten by the provider the
        /// next morning, which is the one thing that would make the manual screen
        /// pointless.
        /// </summary>
        public bool IsPinned { get; private set; }

        // EF materialisation; stored rows bypass the checks below.
        private ExchangeRate() { }

        public static ExchangeRate Create(string from, string to, decimal rate, DateTime asOf)
        {
            from = Normalise(from, nameof(FromCurrency));
            to = Normalise(to, nameof(ToCurrency));

            if (from == to)
            {
                throw new DomainRuleException(nameof(ToCurrency),
                    "A rate converts between two different currencies.");
            }

            RequireRate(rate);

            return new ExchangeRate { FromCurrency = from, ToCurrency = to, Rate = rate, AsOf = asOf };
        }

        /// <summary>
        /// Re-quotes an existing pair by hand. The pair itself never changes.
        /// Clears <see cref="RefreshedAt"/>: from here on this figure is a
        /// person's, not the provider's.
        /// </summary>
        public void Amend(decimal rate, DateTime asOf)
        {
            RequireRate(rate);

            Rate = rate;
            AsOf = asOf;
            RefreshedAt = null;
        }

        /// <summary>
        /// Re-quotes the pair from the automatic feed. Separate from
        /// <see cref="Amend"/> so the two origins stay distinguishable — the
        /// screen says which, and a pinned pair never reaches this at all
        /// (see <c>ExchangeRateRefreshJob</c>).
        /// </summary>
        public void Refresh(decimal rate, DateTime asOf, DateTimeOffset refreshedAt)
        {
            RequireRate(rate);

            Rate = rate;
            AsOf = asOf;
            RefreshedAt = refreshedAt;
        }

        public void SetPinned(bool pinned) => IsPinned = pinned;

        private static string Normalise(string currency, string field)
        {
            if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            {
                throw new DomainRuleException(field,
                    "A currency is a 3-letter ISO 4217 code.");
            }

            return currency.Trim().ToUpperInvariant();
        }

        private static void RequireRate(decimal rate)
        {
            // Zero would price everything at nothing and a negative rate has no
            // meaning; both would reach a visitor as a plausible-looking figure.
            if (rate <= 0m)
            {
                throw new DomainRuleException(nameof(Rate), "A rate is greater than zero.");
            }
        }
    }
}
