namespace RemSolution.Domain.ValueObjects;

/// <summary>
/// A monetary amount together with its ISO 4217 currency. Currency is
/// tenant-scoped: an agency operates in a single currency (see
/// <see cref="Entities.Agency.Currency"/>), and every amount an agency stores
/// carries that currency so a value is never ambiguous once detached from its
/// row. Arithmetic is only defined between amounts of the same currency —
/// mixing currencies throws rather than silently producing a wrong total.
/// </summary>
public sealed class Money : ValueObject
{
    // Private so the only way in is Of(...), which validates the currency. EF
    // materialises via this constructor by parameter name (amount, currency);
    // stored values are already valid, so they bypass the factory's checks.
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    /// <summary>ISO 4217 code, upper-case (e.g. "TND", "EUR", "USD").</summary>
    public string Currency { get; }

    public static Money Of(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required.", nameof(currency));
        }

        currency = currency.Trim().ToUpperInvariant();

        if (currency.Length != 3)
        {
            throw new ArgumentException(
                "Currency must be a 3-letter ISO 4217 code.", nameof(currency));
        }

        return new Money(amount, currency);
    }

    public static Money Zero(string currency) => Of(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    /// <summary>Rounds to <paramref name="decimals"/> places, away from zero.</summary>
    public Money Round(int decimals = 2) =>
        new(Math.Round(Amount, decimals, MidpointRounding.AwayFromZero), Currency);

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public static Money operator *(Money money, decimal factor) => money.Multiply(factor);

    private void EnsureSameCurrency(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Currency != other.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot combine Money of different currencies ({Currency} and {other.Currency}).");
        }
    }

    public override string ToString() => $"{Amount:0.00} {Currency}";

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
