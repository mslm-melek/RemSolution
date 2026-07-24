using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Domain.UnitTests.ValueObjects;

public class MoneyTests
{
    [Test]
    public void Of_NormalisesCurrencyToUpperCase()
    {
        var money = Money.Of(10m, "eur");

        money.Currency.Should().Be("EUR");
        money.Amount.Should().Be(10m);
    }

    [TestCase("")]
    [TestCase("  ")]
    [TestCase("EU")]
    [TestCase("EURO")]
    public void Of_Throws_ForInvalidCurrency(string currency)
    {
        var act = () => Money.Of(10m, currency);

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Add_SumsSameCurrency()
    {
        var result = Money.Of(10m, "TND") + Money.Of(5m, "TND");

        result.Should().Be(Money.Of(15m, "TND"));
    }

    [Test]
    public void Add_Throws_ForDifferentCurrencies()
    {
        var act = () => Money.Of(10m, "TND") + Money.Of(5m, "EUR");

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Multiply_ScalesAmountKeepingCurrency()
    {
        var result = Money.Of(10m, "USD") * 3;

        result.Should().Be(Money.Of(30m, "USD"));
    }

    [Test]
    public void Equality_IsByValue()
    {
        Money.Of(10m, "TND").Should().Be(Money.Of(10m, "TND"));
        Money.Of(10m, "TND").Should().NotBe(Money.Of(10m, "EUR"));
    }

    [Test]
    public void Round_RoundsAwayFromZero()
    {
        Money.Of(10.125m, "TND").Round(2).Should().Be(Money.Of(10.13m, "TND"));
    }
}
