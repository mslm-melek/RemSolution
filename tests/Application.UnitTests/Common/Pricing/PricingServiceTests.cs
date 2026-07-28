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

    // --- RepriceForNewEndDate: the rule the extension flow depends on --------

    [Test]
    public void Reprice_ChargesOnlyTheAddedDays_AtTheCarsCurrentRate()
    {
        // Agreed at 100/day for 5 days; the rate has since gone up to 120.
        var car = new Car { DailyRate = Money.Of(120m, "TND") };
        var start = new DateTime(2026, 1, 1);
        var agreed = Money.Of(500m, "TND");

        var price = _sut.RepriceForNewEndDate(car, agreed, start, start.AddDays(5), start.AddDays(8));

        // 500 kept + 3 new days at today's 120 — NOT 8 × 120.
        price.Should().Be(Money.Of(860m, "TND"));
    }

    [Test]
    public void Reprice_DoesNotReopenTheAgreedPart_WhenTheRateFell()
    {
        // The rate dropped after the booking; the agreed days stay agreed.
        var car = new Car { DailyRate = Money.Of(40m, "TND") };
        var start = new DateTime(2026, 1, 1);
        var agreed = Money.Of(500m, "TND");

        var price = _sut.RepriceForNewEndDate(car, agreed, start, start.AddDays(5), start.AddDays(6));

        price.Should().Be(Money.Of(540m, "TND"));
    }

    [Test]
    public void Reprice_CreditsGivenBackDays_AtTheAgreedRateNotTodays()
    {
        // Agreed at 100/day for 5 days = 500. The car now costs 200/day, which
        // must not inflate the credit for an early return.
        var car = new Car { DailyRate = Money.Of(200m, "TND") };
        var start = new DateTime(2026, 1, 1);
        var agreed = Money.Of(500m, "TND");

        var price = _sut.RepriceForNewEndDate(car, agreed, start, start.AddDays(5), start.AddDays(3));

        // 3 of the 5 agreed days: 500 × 3/5.
        price.Should().Be(Money.Of(300m, "TND"));
    }

    [Test]
    public void Reprice_RoundsTheProRataCreditToTwoPlaces()
    {
        var car = new Car { DailyRate = Money.Of(50m, "TND") };
        var start = new DateTime(2026, 1, 1);
        var agreed = Money.Of(500m, "TND");

        // 500 × 5/7 = 357.142857…
        var price = _sut.RepriceForNewEndDate(car, agreed, start, start.AddDays(7), start.AddDays(5));

        price.Should().Be(Money.Of(357.14m, "TND"));
    }

    [Test]
    public void Reprice_KeepsTheAgreedPrice_WhenTheBilledDaysDoNotChange()
    {
        var car = new Car { DailyRate = Money.Of(999m, "TND") };
        var start = new DateTime(2026, 1, 1);
        var agreed = Money.Of(500m, "TND");

        // Both ends fall inside the same billed day (a started day bills in full).
        var price = _sut.RepriceForNewEndDate(
            car, agreed, start, start.AddDays(4).AddHours(2), start.AddDays(4).AddHours(20));

        price.Should().Be(agreed);
    }

    [Test]
    public void Reprice_Throws_WhenDaysAreAddedAndTheCarHasNoRate()
    {
        var car = new Car { DailyRate = null };
        var start = new DateTime(2026, 1, 1);

        var act = () => _sut.RepriceForNewEndDate(
            car, Money.Of(500m, "TND"), start, start.AddDays(5), start.AddDays(7));

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Reprice_ShortensWithoutARate_BecauseNothingNewIsQuoted()
    {
        // An unpriced car can still take an early return: the credit comes out of
        // what was agreed, so no current rate is needed.
        var car = new Car { DailyRate = null };
        var start = new DateTime(2026, 1, 1);

        var price = _sut.RepriceForNewEndDate(
            car, Money.Of(500m, "TND"), start, start.AddDays(5), start.AddDays(4));

        price.Should().Be(Money.Of(400m, "TND"));
    }

    [Test]
    public void Reprice_Throws_WhenTheNewEndIsNotAfterTheStart()
    {
        var car = new Car { DailyRate = Money.Of(50m, "TND") };
        var start = new DateTime(2026, 1, 1);

        var act = () => _sut.RepriceForNewEndDate(
            car, Money.Of(500m, "TND"), start, start.AddDays(5), start.AddDays(-1));

        act.Should().Throw<ArgumentException>();
    }
}
