using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.Exceptions;

namespace RemSolution.Domain.UnitTests.Cars;

/// <summary>A declared stretch of time off the road, and its own rules.</summary>
public class CarUnavailabilityTests
{
    private static readonly DateTime From = new(2030, 5, 20, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2030, 5, 24, 0, 0, 0, DateTimeKind.Utc);

    private static CarUnavailability ABlock() =>
        CarUnavailability.Create(carId: 1, From, To, CarUnavailabilityReason.Maintenance, "Timing belt");

    [Test]
    public void Create_ShouldDeclareThePeriod()
    {
        var block = ABlock();

        block.CarId.Should().Be(1);
        block.StartDate.Should().Be(From);
        block.EndDate.Should().Be(To);
        block.Reason.Should().Be(CarUnavailabilityReason.Maintenance);
        block.Note.Should().Be("Timing belt");
    }

    [Test]
    public void Create_ShouldRefuseAPeriodThatEndsBeforeItStarts()
    {
        FluentActions.Invoking(() => CarUnavailability.Create(
                1, To, From, CarUnavailabilityReason.Repair))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Create_ShouldRefuseAnEmptyPeriod()
    {
        FluentActions.Invoking(() => CarUnavailability.Create(
                1, From, From, CarUnavailabilityReason.Repair))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Create_ShouldRefuseABlockWithNoCar()
    {
        FluentActions.Invoking(() => CarUnavailability.Create(
                0, From, To, CarUnavailabilityReason.Repair))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Create_ShouldNormaliseABlankNoteToNothing()
    {
        var block = CarUnavailability.Create(1, From, To, CarUnavailabilityReason.Other, "   ");

        block.Note.Should().BeNull();
    }

    [Test]
    public void Create_ShouldRefuseANoteLongerThanTheColumn()
    {
        FluentActions.Invoking(() => CarUnavailability.Create(
                1, From, To, CarUnavailabilityReason.Other,
                new string('x', CarUnavailability.MaxNoteLength + 1)))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Amend_ShouldMoveThePeriodAndKeepTheCar()
    {
        var block = ABlock();

        block.Amend(From.AddDays(1), To.AddDays(3), CarUnavailabilityReason.Repair, "Gearbox");

        block.CarId.Should().Be(1);
        block.StartDate.Should().Be(From.AddDays(1));
        block.EndDate.Should().Be(To.AddDays(3));
        block.Reason.Should().Be(CarUnavailabilityReason.Repair);
        block.Note.Should().Be("Gearbox");
    }

    [Test]
    public void Amend_ShouldRefuseAnInvertedPeriod()
    {
        var block = ABlock();

        FluentActions.Invoking(() => block.Amend(To, From, CarUnavailabilityReason.Repair, null))
            .Should().Throw<DomainRuleException>();
    }

    // The end date is EXCLUSIVE, like every other period in the booking model: a
    // car away "the 20th to the 24th" is free to collect on the 24th.
    [TestCase(18, 20, false, TestName = "Overlaps_EndsWhereTheBlockStarts_DoesNot")]
    [TestCase(24, 26, false, TestName = "Overlaps_StartsWhereTheBlockEnds_DoesNot")]
    [TestCase(19, 21, true, TestName = "Overlaps_StraddlesTheStart_Does")]
    [TestCase(23, 25, true, TestName = "Overlaps_StraddlesTheEnd_Does")]
    [TestCase(21, 22, true, TestName = "Overlaps_SitsInside_Does")]
    [TestCase(1, 30, true, TestName = "Overlaps_SwallowsTheBlock_Does")]
    public void Overlaps_ShouldTreatThePeriodAsHalfOpen(int startDay, int endDay, bool expected)
    {
        var block = ABlock();

        var start = new DateTime(2030, 5, startDay, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2030, 5, endDay, 0, 0, 0, DateTimeKind.Utc);

        block.Overlaps(start, end).Should().Be(expected);
    }
}
