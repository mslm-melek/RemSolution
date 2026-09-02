namespace RemSolution.Domain.ValueObjects;

/// <summary>
/// The tax decomposition of an invoice total: net, tax, gross, duty stamp, and
/// what the client actually owes.
/// <para>
/// Built from the GROSS amount, because that is the direction this product's
/// money runs — an agency enters a tax-inclusive daily rate and the invoice works
/// backwards (see AgencySettings' tax section). The net is rounded and the tax is
/// then the remainder, never rounded independently, so
/// <c>Net + Vat == Gross</c> exactly. Two independently rounded halves that fail
/// to add up to the printed total is the classic invoice defect, and it is the
/// one an accountant notices.
/// </para>
/// </summary>
public sealed class TaxBreakdown : ValueObject
{
    // Every Money column in the schema is decimal(18,2), so the split rounds to
    // the same two places rather than inventing a precision the database would
    // silently truncate.
    private const int Decimals = 2;

    private TaxBreakdown(
        Money net, Money vat, Money gross, Money fiscalStamp, Money totalDue, decimal vatRatePercent)
    {
        Net = net;
        Vat = vat;
        Gross = gross;
        FiscalStamp = fiscalStamp;
        TotalDue = totalDue;
        VatRatePercent = vatRatePercent;
    }

    /// <summary>Net of tax.</summary>
    public Money Net { get; }

    /// <summary>The tax, as <c>Gross − Net</c>.</summary>
    public Money Vat { get; }

    /// <summary>Tax-inclusive total of the invoice lines.</summary>
    public Money Gross { get; }

    public Money FiscalStamp { get; }

    /// <summary><c>Gross + FiscalStamp</c> — what is owed.</summary>
    public Money TotalDue { get; }

    /// <summary>The rate applied, as a percentage (19 means 19%).</summary>
    public decimal VatRatePercent { get; }

    /// <summary>
    /// Splits a tax-inclusive total. A zero or negative rate means no tax is
    /// charged, which is a legitimate configuration (an exempt agency, or one in a
    /// jurisdiction with no VAT) rather than an error: net then equals gross.
    /// </summary>
    /// <param name="gross">Tax-inclusive total of the invoice lines.</param>
    /// <param name="vatRatePercent">Rate as a percentage; 19 means 19%.</param>
    /// <param name="fiscalStampAmount">Flat duty stamp; zero where none applies.</param>
    public static TaxBreakdown FromGross(Money gross, decimal vatRatePercent, decimal fiscalStampAmount)
    {
        ArgumentNullException.ThrowIfNull(gross);

        if (vatRatePercent < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(vatRatePercent), vatRatePercent, "A VAT rate cannot be negative.");
        }

        if (fiscalStampAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fiscalStampAmount), fiscalStampAmount, "A duty stamp cannot be negative.");
        }

        var currency = gross.Currency;

        var net = vatRatePercent == 0m
            ? gross.Amount
            : Math.Round(
                gross.Amount / (1m + (vatRatePercent / 100m)), Decimals, MidpointRounding.AwayFromZero);

        var stamp = Math.Round(fiscalStampAmount, Decimals, MidpointRounding.AwayFromZero);

        return new TaxBreakdown(
            net: Money.Of(net, currency),
            // The remainder, not a second rounding: this is what keeps the three
            // printed figures consistent.
            vat: Money.Of(gross.Amount - net, currency),
            gross: gross,
            fiscalStamp: Money.Of(stamp, currency),
            totalDue: Money.Of(gross.Amount + stamp, currency),
            vatRatePercent: vatRatePercent);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Net;
        yield return Vat;
        yield return Gross;
        yield return FiscalStamp;
        yield return TotalDue;
        yield return VatRatePercent;
    }
}
