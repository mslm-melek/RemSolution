using RemSolution.Domain.Common;
using RemSolution.Domain.Entities;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Domain.Events;

// One event per renting transition, like ReservationEvents. No consumers yet
// beyond logging; they are the seam notifications and the outbox will hook onto.

public class RentingStartedEvent : BaseEvent
{
    public RentingStartedEvent(Renting renting) => Renting = renting;
    public Renting Renting { get; }
}

/// <summary>
/// The RentingHistory snapshot is written by the completing handler, in the same
/// save, so it goes through the tenant and audit interceptors.
/// </summary>
public class RentingCompletedEvent : BaseEvent
{
    public RentingCompletedEvent(Renting renting) => Renting = renting;
    public Renting Renting { get; }
}

public class RentingCancelledEvent : BaseEvent
{
    public RentingCancelledEvent(Renting renting, Money? cancellationFee)
    {
        Renting = renting;
        CancellationFee = cancellationFee;
    }

    public Renting Renting { get; }
    // Null when the hire was cancelled for free.
    public Money? CancellationFee { get; }
}
