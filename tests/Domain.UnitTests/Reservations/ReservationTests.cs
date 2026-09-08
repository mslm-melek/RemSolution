using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.Events;
using RemSolution.Domain.Exceptions;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Domain.UnitTests.Reservations;

/// <summary>
/// The hold's state machine. Its transitions were already guarded; what was
/// missing was anything asserting the guards.
/// </summary>
public class ReservationTests
{
    private static readonly DateTime Start = new(2030, 5, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 5, 5, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Expiry = new(2030, 4, 25, 9, 0, 0, DateTimeKind.Utc);

    private static Reservation AHold() =>
        Reservation.Create(carId: 1, startDate: Start, endDate: End,
            price: Money.Of(400m, "TND"), expiresAt: Expiry, clientId: 2);

    private static Renting AHire() =>
        Renting.Create(carId: 1, clientId: 2, startDate: Start, endDate: End, price: null);

    [Test]
    public void Create_ShouldOpenAPendingHold()
    {
        var hold = AHold();

        hold.Status.Should().Be(ReservationStatus.PendingConfirmation);
        hold.CarId.Should().Be(1);
        hold.ClientId.Should().Be(2);
        hold.ExpiresAt.Should().Be(Expiry);
        hold.RentingId.Should().BeNull();
        hold.DomainEvents.Should().BeEmpty();
    }

    [Test]
    public void Confirm_ShouldMoveAPendingHoldOn()
    {
        var hold = AHold();

        hold.Confirm();

        hold.Status.Should().Be(ReservationStatus.Confirmed);
        hold.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ReservationConfirmedEvent>();
    }

    [Test]
    public void Confirm_ShouldRefuseAHoldThatHasAlreadyMovedOn()
    {
        var hold = AHold();
        hold.Cancel("Changed their mind.");

        FluentActions.Invoking(() => hold.Confirm())
            .Should().Throw<InvalidReservationTransitionException>()
            .Which.From.Should().Be(ReservationStatus.Cancelled);
    }

    // The reason is shown to the client.
    [Test]
    public void Reject_ShouldRequireAReason()
    {
        var hold = AHold();

        FluentActions.Invoking(() => hold.Reject("  ")).Should().Throw<ArgumentException>();

        hold.Status.Should().Be(ReservationStatus.PendingConfirmation);
    }

    [Test]
    public void Reject_ShouldRecordWhyTheAgencyDeclined()
    {
        var hold = AHold();

        hold.Reject("That car is spoken for.");

        hold.Status.Should().Be(ReservationStatus.Rejected);
        hold.RejectedReason.Should().Be("That car is spoken for.");
    }

    [Test]
    public void Cancel_ShouldBeAllowedRightUpToConversion()
    {
        var pending = AHold();
        pending.Cancel(null);
        pending.Status.Should().Be(ReservationStatus.Cancelled);

        var confirmed = AHold();
        confirmed.Confirm();
        confirmed.Cancel("Client called it off.");
        confirmed.Status.Should().Be(ReservationStatus.Cancelled);
        confirmed.CancelledReason.Should().Be("Client called it off.");

        var paid = AHold();
        paid.Confirm();
        paid.MarkPaid();
        paid.Cancel(null);
        paid.Status.Should().Be(ReservationStatus.Cancelled);
    }

    /// <summary>
    /// The flag both reliability scores read. Status is gone by the time anyone
    /// looks, so getting this wrong is the difference between "the agency broke
    /// a promise" and "a request went unanswered".
    /// </summary>
    [Test]
    public void Cancel_ShouldRecordWhetherTheHoldHadBeenConfirmed()
    {
        var pending = AHold();
        pending.Cancel(null);
        pending.CancelledAfterConfirmation.Should().BeFalse();

        var confirmed = AHold();
        confirmed.Confirm();
        confirmed.Cancel("The car was written off.");
        confirmed.CancelledAfterConfirmation.Should().BeTrue();

        var paid = AHold();
        paid.Confirm();
        paid.MarkPaid();
        paid.Cancel(null);
        paid.CancelledAfterConfirmation.Should().BeTrue();
    }

    [Test]
    public void Cancel_ShouldRefuseAHoldThatIsAlreadyAHire()
    {
        var hold = AHold();
        hold.Confirm();
        hold.Convert(AHire());

        FluentActions.Invoking(() => hold.Cancel(null))
            .Should().Throw<InvalidReservationTransitionException>()
            .Which.From.Should().Be(ReservationStatus.Converted);
    }

    [Test]
    public void Expire_ShouldOnlyLapseAHoldNobodyAnswered()
    {
        var pending = AHold();
        pending.Expire();
        pending.Status.Should().Be(ReservationStatus.Expired);
        pending.ExpiredReason.Should().NotBeNullOrWhiteSpace();

        var confirmed = AHold();
        confirmed.Confirm();

        FluentActions.Invoking(() => confirmed.Expire())
            .Should().Throw<InvalidReservationTransitionException>();
    }

    [Test]
    public void MarkPaid_ShouldOnlyFollowAConfirmation()
    {
        var hold = AHold();

        FluentActions.Invoking(() => hold.MarkPaid())
            .Should().Throw<InvalidReservationTransitionException>()
            .Which.From.Should().Be(ReservationStatus.PendingConfirmation);

        hold.Confirm();
        hold.MarkPaid();
        hold.Status.Should().Be(ReservationStatus.Paid);
    }

    [Test]
    public void Convert_ShouldLinkTheHireItBecame()
    {
        var hold = AHold();
        hold.Confirm();
        hold.ClearDomainEvents();

        var renting = AHire();
        hold.Convert(renting);

        hold.Status.Should().Be(ReservationStatus.Converted);
        hold.Renting.Should().BeSameAs(renting);
        hold.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ReservationConvertedEvent>();
    }

    // Confirming is not converting: the agency has not agreed to it yet.
    [Test]
    public void Convert_ShouldRefuseAHoldNobodyHasConfirmed()
    {
        var hold = AHold();

        FluentActions.Invoking(() => hold.Convert(AHire()))
            .Should().Throw<InvalidReservationTransitionException>()
            .Which.From.Should().Be(ReservationStatus.PendingConfirmation);
    }

    [Test]
    public void ShouldWalkTheHappyPathFromRequestToHire()
    {
        var hold = AHold();

        hold.Confirm();
        hold.MarkPaid();
        hold.Convert(AHire());

        hold.Status.Should().Be(ReservationStatus.Converted);
        hold.DomainEvents.Select(e => e.GetType()).Should().Equal(
            typeof(ReservationConfirmedEvent),
            typeof(ReservationPaidEvent),
            typeof(ReservationConvertedEvent));
    }
}
