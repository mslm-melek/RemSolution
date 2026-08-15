using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.Events;
using RemSolution.Domain.Exceptions;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Domain.UnitTests.Rentings;

/// <summary>The hire's own rules — each one used to be a line in a handler.</summary>
public class RentingTests
{
    private static readonly DateTime Start = new(2030, 5, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 5, 5, 9, 0, 0, DateTimeKind.Utc);

    private static Renting AHire(Money? price = null, int? startMileage = null) =>
        Renting.Create(carId: 1, clientId: 2, startDate: Start, endDate: End,
            price: price ?? Money.Of(400m, "TND"), startMileage: startMileage);

    [Test]
    public void Create_ShouldOpenAnUpcomingHire()
    {
        var renting = Renting.Create(
            carId: 1, clientId: 2, startDate: Start, endDate: End,
            price: Money.Of(400m, "TND"), startMileage: 12_000,
            secondClientId: 3, depositAmount: Money.Of(300m, "TND"), notes: "Airport pickup");

        renting.RentingState.Should().Be(RentingState.NotYet);
        renting.CarId.Should().Be(1);
        renting.ClientId.Should().Be(2);
        renting.SecondClientId.Should().Be(3);
        renting.StartDate.Should().Be(Start);
        renting.EndDate.Should().Be(End);
        renting.StartMileage.Should().Be(12_000);
        renting.EndMileage.Should().BeNull();
        renting.Price.Should().Be(Money.Of(400m, "TND"));
        renting.DepositAmount.Should().Be(Money.Of(300m, "TND"));
        renting.CancellationFee.Should().BeNull();
        renting.Notes.Should().Be("Airport pickup");
    }

    // A courtesy car is a real booking at no charge.
    [Test]
    public void Create_ShouldAllowNoPriceButNotANegativeOne()
    {
        Renting.Create(1, 2, Start, End, price: null).Price.Should().BeNull();

        FluentActions.Invoking(() => Renting.Create(1, 2, Start, End, Money.Of(-1m, "TND")))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Create_ShouldRejectAPeriodThatDoesNotRunForwards()
    {
        FluentActions.Invoking(() => Renting.Create(1, 2, End, Start, null))
            .Should().Throw<DomainRuleException>()
            .Which.Property.Should().Be(nameof(Renting.EndDate));

        // Same instant twice is not a period either.
        FluentActions.Invoking(() => Renting.Create(1, 2, Start, Start, null))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Create_ShouldRejectAHireWithNoCarOrNoClient()
    {
        FluentActions.Invoking(() => Renting.Create(0, 2, Start, End, null))
            .Should().Throw<DomainRuleException>();

        FluentActions.Invoking(() => Renting.Create(1, 0, Start, End, null))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Create_ShouldRejectASecondDriverWhoIsTheRenter()
    {
        FluentActions.Invoking(() => Renting.Create(1, 2, Start, End, null, secondClientId: 2))
            .Should().Throw<DomainRuleException>()
            .Which.Property.Should().Be(nameof(Renting.SecondClientId));
    }

    [Test]
    public void Create_ShouldRejectANegativeMileage()
    {
        FluentActions.Invoking(() => Renting.Create(1, 2, Start, End, null, startMileage: -1))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Start_ShouldTakeTheHireOutAndRecordThePickupReading()
    {
        var renting = AHire();

        renting.Start(15_000);

        renting.RentingState.Should().Be(RentingState.InProgress);
        renting.StartMileage.Should().Be(15_000);
        renting.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RentingStartedEvent>();
    }

    // Nothing typed at the counter leaves whatever the booking already carried.
    [Test]
    public void Start_ShouldKeepTheBookedReadingWhenNoneIsTaken()
    {
        var renting = AHire(startMileage: 9_000);

        renting.Start();

        renting.StartMileage.Should().Be(9_000);
    }

    [Test]
    public void Start_ShouldRefuseAHireThatIsAlreadyOutOrClosed()
    {
        var running = AHire();
        running.Start();

        FluentActions.Invoking(() => running.Start())
            .Should().Throw<InvalidRentingTransitionException>()
            .Which.From.Should().Be(RentingState.InProgress);

        var cancelled = AHire();
        cancelled.Cancel();

        FluentActions.Invoking(() => cancelled.Start())
            .Should().Throw<InvalidRentingTransitionException>();
    }

    [Test]
    public void Complete_ShouldCloseTheHireAndRecordTheReturnReading()
    {
        var renting = AHire();
        renting.Start(15_000);
        renting.ClearDomainEvents();

        renting.Complete(15_600, DateTime.UtcNow);

        renting.RentingState.Should().Be(RentingState.Done);
        renting.EndMileage.Should().Be(15_600);
        renting.EndDate.Should().Be(End);
        renting.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RentingCompletedEvent>();
    }

    [Test]
    public void Complete_ShouldRefuseAReturnReadingBelowThePickupOne()
    {
        var renting = AHire();
        renting.Start(15_000);

        FluentActions.Invoking(() => renting.Complete(14_999, DateTime.UtcNow))
            .Should().Throw<DomainRuleException>()
            .Which.Property.Should().Be(nameof(Renting.EndMileage));

        // The refused call left the hire exactly as it was.
        renting.RentingState.Should().Be(RentingState.InProgress);
        renting.EndMileage.Should().BeNull();
    }

    // An unread odometer is not a claim about distance, so it blocks nothing.
    [Test]
    public void Complete_ShouldAcceptAReturnReadingWhenThePickupOneIsMissing()
    {
        var renting = AHire();
        renting.Start();

        renting.Complete(400, DateTime.UtcNow);

        renting.EndMileage.Should().Be(400);
    }

    [Test]
    public void Complete_ShouldRefuseAHireThatWasNeverStarted()
    {
        var renting = AHire();

        FluentActions.Invoking(() => renting.Complete(100, DateTime.UtcNow))
            .Should().Throw<InvalidRentingTransitionException>()
            .Which.From.Should().Be(RentingState.NotYet);
    }

    [Test]
    public void Complete_ShouldRefuseAHireThatIsAlreadyBack()
    {
        var renting = AHire();
        renting.Start();
        renting.Complete(null, DateTime.UtcNow);

        FluentActions.Invoking(() => renting.Complete(null, DateTime.UtcNow))
            .Should().Throw<InvalidRentingTransitionException>();
    }

    [Test]
    public void Cancel_ShouldCancelForFreeByDefault()
    {
        var renting = AHire();

        renting.Cancel();

        renting.RentingState.Should().Be(RentingState.Cancelled);
        renting.CancellationFee.Should().BeNull();
        renting.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RentingCancelledEvent>();
    }

    [Test]
    public void Cancel_ShouldKeepTheFeeTheAgencyCharged()
    {
        var renting = AHire(Money.Of(400m, "TND"));

        renting.Cancel(Money.Of(50m, "TND"));

        renting.CancellationFee.Should().Be(Money.Of(50m, "TND"));
    }

    // A zero fee must not leave a 0.00 charge on the client's account.
    [Test]
    public void Cancel_ShouldTreatAZeroFeeAsNoFee()
    {
        var renting = AHire();

        renting.Cancel(Money.Of(0m, "TND"));

        renting.CancellationFee.Should().BeNull();
    }

    [Test]
    public void Cancel_ShouldRefuseAFeeAboveThePrice()
    {
        var renting = AHire(Money.Of(400m, "TND"));

        FluentActions.Invoking(() => renting.Cancel(Money.Of(400.01m, "TND")))
            .Should().Throw<DomainRuleException>();

        renting.RentingState.Should().Be(RentingState.NotYet);
    }

    [Test]
    public void Cancel_ShouldRefuseAFeeOnAHireThatChargesNothing()
    {
        // No price for a fee to be a part of.
        var renting = Renting.Create(1, 2, Start, End, price: null);

        FluentActions.Invoking(() => renting.Cancel(Money.Of(10m, "TND")))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Cancel_ShouldRefuseAFeeInAnotherCurrency()
    {
        var renting = AHire(Money.Of(400m, "TND"));

        FluentActions.Invoking(() => renting.Cancel(Money.Of(10m, "EUR")))
            .Should().Throw<DomainRuleException>();
    }

    [Test]
    public void Cancel_ShouldRefuseAFinishedHire()
    {
        var renting = AHire();
        renting.Start();
        renting.Complete(null, DateTime.UtcNow);

        FluentActions.Invoking(() => renting.Cancel())
            .Should().Throw<InvalidRentingTransitionException>()
            .Which.From.Should().Be(RentingState.Done);
    }

    // The client rings up mid-hire and the agency calls the rest of it off.
    [Test]
    public void Cancel_ShouldBeAllowedWhileTheCarIsOut()
    {
        var renting = AHire();
        renting.Start();

        renting.Cancel();

        renting.RentingState.Should().Be(RentingState.Cancelled);
    }

    [Test]
    public void Amend_ShouldCorrectEveryEditableFieldAtOnce()
    {
        var renting = AHire();
        var newStart = Start.AddDays(1);
        var newEnd = End.AddDays(2);

        renting.Amend(
            carId: 9, clientId: 8, secondClientId: 7,
            startDate: newStart, endDate: newEnd,
            startMileage: 100, endMileage: 900,
            price: Money.Of(600m, "TND"), notes: "Moved to the estate");

        renting.CarId.Should().Be(9);
        renting.ClientId.Should().Be(8);
        renting.SecondClientId.Should().Be(7);
        renting.StartDate.Should().Be(newStart);
        renting.EndDate.Should().Be(newEnd);
        renting.StartMileage.Should().Be(100);
        renting.EndMileage.Should().Be(900);
        renting.Price.Should().Be(Money.Of(600m, "TND"));
        renting.Notes.Should().Be("Moved to the estate");
        // An edit is not a transition: an upcoming hire stays upcoming.
        renting.RentingState.Should().Be(RentingState.NotYet);
    }

    [Test]
    public void Amend_ShouldRefuseReadingsThatRunBackwards()
    {
        var renting = AHire();

        FluentActions.Invoking(() => renting.Amend(
                1, 2, null, Start, End, startMileage: 900, endMileage: 100,
                price: null, notes: null))
            .Should().Throw<DomainRuleException>()
            .Which.Property.Should().Be(nameof(Renting.EndMileage));
    }

    [Test]
    public void Amend_ShouldRefuseAClosedHire()
    {
        var renting = AHire();
        renting.Start();
        renting.Complete(null, DateTime.UtcNow);

        FluentActions.Invoking(() => renting.Amend(
                1, 2, null, Start, End, null, null, null, null))
            .Should().Throw<InvalidRentingTransitionException>();
    }

    [Test]
    public void ChangeEndDate_ShouldMoveTheEndAndTheAgreedPrice()
    {
        var renting = AHire(Money.Of(400m, "TND"));
        var extended = End.AddDays(2);

        renting.ChangeEndDate(extended, Money.Of(600m, "TND"));

        renting.EndDate.Should().Be(extended);
        renting.Price.Should().Be(Money.Of(600m, "TND"));
        // The rest is untouched — that is what makes it not a full edit.
        renting.StartDate.Should().Be(Start);
        renting.CarId.Should().Be(1);
    }

    // Bringing the car back early is the same command, and stops at the start.
    [Test]
    public void ChangeEndDate_ShouldRefuseAnEndBeforeTheStart()
    {
        var renting = AHire();

        renting.ChangeEndDate(Start.AddHours(1), null);
        renting.EndDate.Should().Be(Start.AddHours(1));

        FluentActions.Invoking(() => renting.ChangeEndDate(Start.AddHours(-1), null))
            .Should().Throw<DomainRuleException>()
            .Which.Property.Should().Be(nameof(Renting.EndDate));
    }

    [Test]
    public void ChangeEndDate_ShouldRefuseACancelledHire()
    {
        var renting = AHire();
        renting.Cancel();

        FluentActions.Invoking(() => renting.ChangeEndDate(End.AddDays(1), null))
            .Should().Throw<InvalidRentingTransitionException>()
            .Which.From.Should().Be(RentingState.Cancelled);
    }

    [Test]
    public void ShouldWalkTheHappyPathFromBookingToReturn()
    {
        var renting = AHire(Money.Of(400m, "TND"));

        renting.RentingState.Should().Be(RentingState.NotYet);
        renting.Start(20_000);
        renting.RentingState.Should().Be(RentingState.InProgress);
        renting.ChangeEndDate(End.AddDays(1), Money.Of(500m, "TND"));
        renting.Complete(20_650, DateTime.UtcNow);

        renting.RentingState.Should().Be(RentingState.Done);
        renting.StartMileage.Should().Be(20_000);
        renting.EndMileage.Should().Be(20_650);
        renting.Price.Should().Be(Money.Of(500m, "TND"));
        renting.DomainEvents.Select(e => e.GetType()).Should().Equal(
            typeof(RentingStartedEvent), typeof(RentingCompletedEvent));
    }

    // The agreed end date is what was billed; the clock at the counter is not.
    [Test]
    public void Complete_ShouldNotOverwriteTheAgreedEndDate()
    {
        var renting = AHire();
        renting.Start();

        var now = new DateTime(2030, 6, 6, 12, 0, 0, DateTimeKind.Utc);
        renting.Complete(null, now);

        renting.EndDate.Should().Be(End);
    }
}
