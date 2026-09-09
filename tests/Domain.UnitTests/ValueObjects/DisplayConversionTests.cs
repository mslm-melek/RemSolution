using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Domain.UnitTests.ValueObjects;

/// <summary>
/// The courtesy conversion printed beside an invoice total. What matters is that
/// it rounds to the TARGET currency's own minor unit — two is not the universal
/// answer, and the dinar is one of the exceptions.
/// </summary>
public class DisplayConversionTests
{
    private static readonly Money ThousandDinars = Money.Of(1000m, "TND");

    [Test]
    public void ShouldMultiplyAndKeepTheTargetCurrency()
    {
        var converted = DisplayConversion.Apply(ThousandDinars, 0.296247m, "EUR");

        converted.Currency.Should().Be("EUR");
        converted.Amount.Should().Be(296.25m, "two places for the euro, rounded away from zero");
    }

    [Test]
    public void ShouldRoundToThreePlacesForAThousandthsCurrency()
    {
        // The reason CurrencyDecimals exists: 1000 millimes to the dinar, not 100
        // centimes, so a figure read in TND keeps three places.
        var converted = DisplayConversion.Apply(Money.Of(100m, "EUR"), 3.375492m, "TND");

        converted.Amount.Should().Be(337.549m);
    }

    [Test]
    public void ShouldRoundToWholeUnitsForACurrencyWithNoMinorUnit()
    {
        // Printing "≈ 34 512,00 JPY" would invent a subdivision the yen has not
        // had since 1953.
        var converted = DisplayConversion.Apply(ThousandDinars, 34.5117m, "JPY");

        converted.Amount.Should().Be(34512m);
        converted.Amount.Should().Be(Math.Truncate(converted.Amount));
    }

    [Test]
    public void ShouldRefuseANonPositiveRate()
    {
        var zero = () => DisplayConversion.Apply(ThousandDinars, 0m, "EUR");
        var negative = () => DisplayConversion.Apply(ThousandDinars, -0.3m, "EUR");

        zero.Should().Throw<ArgumentOutOfRangeException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    // The whole point of freezing the rate on the invoice: the same rate has to
    // give the same figure however many times the document is reopened.
    [Test]
    public void ShouldBeReproducible()
    {
        var first = DisplayConversion.Apply(ThousandDinars, 0.296247m, "EUR");
        var second = DisplayConversion.Apply(ThousandDinars, 0.296247m, "EUR");

        second.Should().Be(first);
    }
}

/// <summary>
/// The minor-unit lookup. Only the exceptions are worth testing — the default is
/// what everything else falls back to.
/// </summary>
public class CurrencyDecimalsTests
{
    [TestCase("EUR")]
    [TestCase("USD")]
    [TestCase("MAD")]
    [TestCase("AED")]
    public void OrdinaryCurrencies_ShouldHaveTwo(string code) =>
        CurrencyDecimals.For(code).Should().Be(2);

    [TestCase("TND")]
    [TestCase("KWD")]
    [TestCase("BHD")]
    public void ThousandthsCurrencies_ShouldHaveThree(string code) =>
        CurrencyDecimals.For(code).Should().Be(3);

    [TestCase("JPY")]
    [TestCase("XOF")]
    [TestCase("KRW")]
    public void CurrenciesWithNoMinorUnit_ShouldHaveNone(string code) =>
        CurrencyDecimals.For(code).Should().Be(0);

    [Test]
    public void ShouldBeCaseInsensitive() =>
        CurrencyDecimals.For("tnd").Should().Be(3);

    [Test]
    public void AnUnknownOrAbsentCode_ShouldFallBackToTwo()
    {
        // An unrecognised code is far likelier to be a two-decimal currency than
        // something worth failing a document over.
        CurrencyDecimals.For("ZZZ").Should().Be(2);
        CurrencyDecimals.For(null).Should().Be(2);
        CurrencyDecimals.For("  ").Should().Be(2);
    }
}
