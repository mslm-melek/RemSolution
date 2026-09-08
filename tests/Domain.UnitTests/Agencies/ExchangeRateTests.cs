using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Exceptions;

namespace RemSolution.Domain.UnitTests.Agencies;

/// <summary>
/// A quoted rate. Nothing stored is converted by one, but a visitor reads a
/// price through it — so a zero, a negative or a currency quoted against itself
/// would reach them as a plausible-looking figure.
/// </summary>
public class ExchangeRateTests
{
    private static readonly DateTime AsOf = new(2030, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Create_ShouldQuoteThePair()
    {
        var rate = ExchangeRate.Create("tnd", "eur", 0.294118m, AsOf);

        // Normalised, so a lookup by code never misses on case or spacing.
        rate.FromCurrency.Should().Be("TND");
        rate.ToCurrency.Should().Be("EUR");
        rate.Rate.Should().Be(0.294118m);
        rate.AsOf.Should().Be(AsOf);
    }

    [Test]
    public void Create_ShouldRefuseACurrencyAgainstItself()
    {
        FluentActions.Invoking(() => ExchangeRate.Create("EUR", "eur", 1m, AsOf))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Create_ShouldRefuseSomethingThatIsNotACode()
    {
        FluentActions.Invoking(() => ExchangeRate.Create("EURO", "TND", 1m, AsOf))
            .Should().Throw<DomainRuleException>();

        FluentActions.Invoking(() => ExchangeRate.Create("", "TND", 1m, AsOf))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Create_ShouldRefuseARateThatIsNotPositive()
    {
        FluentActions.Invoking(() => ExchangeRate.Create("TND", "EUR", 0m, AsOf))
            .Should().Throw<DomainRuleException>();

        FluentActions.Invoking(() => ExchangeRate.Create("TND", "EUR", -0.3m, AsOf))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Amend_ShouldRequoteWithoutMovingThePair()
    {
        var rate = ExchangeRate.Create("TND", "EUR", 0.29m, AsOf);

        rate.Amend(0.31m, AsOf.AddDays(30));

        rate.Rate.Should().Be(0.31m);
        rate.AsOf.Should().Be(AsOf.AddDays(30));
        rate.FromCurrency.Should().Be("TND");
        rate.ToCurrency.Should().Be("EUR");
    }

    [Test]
    public void Amend_ShouldHoldTheSameRuleAsCreate()
    {
        var rate = ExchangeRate.Create("TND", "EUR", 0.29m, AsOf);

        FluentActions.Invoking(() => rate.Amend(0m, AsOf))
            .Should().Throw<DomainRuleException>();

        rate.Rate.Should().Be(0.29m);
    }
}
