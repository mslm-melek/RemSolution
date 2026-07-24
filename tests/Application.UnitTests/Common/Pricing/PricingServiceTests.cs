using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.Entities;
using RemSolution.Domain.ValueObjects;
using RemSolution.Infrastructure.Pricing;

namespace RemSolution.Application.UnitTests.Common.Pricing;

public class PricingServiceTests
{
    private readonly PricingService _sut = new();

    [Test]
    public void CalculateRentalPrice_MultipliesDailyRateByWholeDays()
    {
        var car = new Car { DailyRate = Money.Of(50m, "TND") };
        var start = new DateTime(2026, 1, 1);

        var price = _sut.CalculateRentalPrice(car, start, start.AddDays(3));

        price.Should().Be(Money.Of(150m, "TND"));
    }

    [Test]
    public void CalculateRentalPrice_KeepsTheCarsCurrency()
    {
        var car = new Car { DailyRate = Money.Of(40m, "EUR") };
        var start = new DateTime(2026, 1, 1);

        var price = _sut.CalculateRentalPrice(car, start, start.AddDays(2));

        price.Currency.Should().Be("EUR");
        price.Amount.Should().Be(80m);
    }

    [Test]
    public void CalculateRentalPrice_BillsAStartedDayInFull()
    {
        var car = new Car { DailyRate = Money.Of(50m, "TND") };
        var start = new DateTime(2026, 1, 1);

        // 2.5 days rounds up to 3 billed days.
        var price = _sut.CalculateRentalPrice(car, start, start.AddDays(2).AddHours(12));

        price.Should().Be(Money.Of(150m, "TND"));
    }

    [Test]
    public void CalculateRentalPrice_Throws_WhenCarHasNoDailyRate()
    {
        var car = new Car { DailyRate = null };
        var start = new DateTime(2026, 1, 1);

        var act = () => _sut.CalculateRentalPrice(car, start, start.AddDays(1));

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void CalculateRentalPrice_Throws_WhenPeriodIsNotPositive()
    {
        var car = new Car { DailyRate = Money.Of(50m, "TND") };
        var start = new DateTime(2026, 1, 1);

        var act = () => _sut.CalculateRentalPrice(car, start, start);

        act.Should().Throw<ArgumentException>();
    }
}
