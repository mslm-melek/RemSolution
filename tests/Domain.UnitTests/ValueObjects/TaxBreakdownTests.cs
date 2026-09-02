using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Domain.UnitTests.ValueObjects;

/// <summary>
/// The invoice arithmetic. Every case here is one an accountant would check.
/// </summary>
public class TaxBreakdownTests
{
    [Test]
    public void FromGross_ShouldSplitATaxInclusiveTotal()
    {
        // 360 TTC at 19 % → 302.52 HT, and the tax is the remainder.
        var breakdown = TaxBreakdown.FromGross(Money.Of(360m, "TND"), 19m, 1m);

        breakdown.Gross.Should().Be(Money.Of(360m, "TND"));
        breakdown.Net.Should().Be(Money.Of(302.52m, "TND"));
        breakdown.Vat.Should().Be(Money.Of(57.48m, "TND"));
        breakdown.VatRatePercent.Should().Be(19m);
    }

    /// <summary>
    /// The property the whole class exists for: two independently rounded halves
    /// that fail to add up to the printed total is the classic invoice defect.
    /// </summary>
    [TestCase(360)]
    [TestCase(1)]
    [TestCase(99.99)]
    [TestCase(1234.56)]
    [TestCase(0.01)]
    public void NetPlusVat_ShouldAlwaysEqualGross(decimal gross)
    {
        var breakdown = TaxBreakdown.FromGross(Money.Of(gross, "TND"), 19m, 1m);

        (breakdown.Net + breakdown.Vat).Should().Be(breakdown.Gross);
    }

    [Test]
    public void TotalDue_ShouldAddTheDutyStamp()
    {
        var breakdown = TaxBreakdown.FromGross(Money.Of(360m, "TND"), 19m, 1m);

        breakdown.FiscalStamp.Should().Be(Money.Of(1m, "TND"));
        breakdown.TotalDue.Should().Be(Money.Of(361m, "TND"));
    }

    [Test]
    public void ZeroRate_ShouldChargeNoTax()
    {
        // An exempt agency, or one in a jurisdiction with no VAT.
        var breakdown = TaxBreakdown.FromGross(Money.Of(360m, "TND"), 0m, 0m);

        breakdown.Net.Should().Be(Money.Of(360m, "TND"));
        breakdown.Vat.Should().Be(Money.Of(0m, "TND"));
        breakdown.TotalDue.Should().Be(Money.Of(360m, "TND"));
    }

    [Test]
    public void EveryAmount_ShouldKeepTheGrossCurrency()
    {
        var breakdown = TaxBreakdown.FromGross(Money.Of(120m, "EUR"), 20m, 0m);

        breakdown.Net.Currency.Should().Be("EUR");
        breakdown.Vat.Currency.Should().Be("EUR");
        breakdown.FiscalStamp.Currency.Should().Be("EUR");
        breakdown.TotalDue.Currency.Should().Be("EUR");
    }

    [Test]
    public void NegativeRate_ShouldBeRefused()
    {
        FluentActions.Invoking(() => TaxBreakdown.FromGross(Money.Of(100m, "TND"), -1m, 0m))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void NegativeStamp_ShouldBeRefused()
    {
        FluentActions.Invoking(() => TaxBreakdown.FromGross(Money.Of(100m, "TND"), 19m, -1m))
            .Should().Throw<ArgumentOutOfRangeException>();
    }
}
