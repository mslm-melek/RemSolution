using RemSolution.Domain.Common;
using RemSolution.Domain.Entities;

namespace RemSolution.Domain.Events;

// The reservation lifecycle raises a domain event on every state transition.
// None have consumers yet beyond the audit/logging pipeline, but they make the
// aggregate the single source of truth for "what happened" and give later
// features (notifications, analytics) a seam to hook into. Dispatch runs before
// the tenant/audit interceptors, same as RentingCompletedEvent.

/// <summary>Raised when a pending hold is confirmed by the agency.</summary>
public class ReservationConfirmedEvent : BaseEvent
{
    public ReservationConfirmedEvent(Reservation reservation) => Reservation = reservation;
    public Reservation Reservation { get; }
}

/// <summary>Raised when the agency rejects a pending hold; carries the reason shown to the client.</summary>
public class ReservationRejectedEvent : BaseEvent
{
    public ReservationRejectedEvent(Reservation reservation, string reason)
    {
        Reservation = reservation;
        Reason = reason;
    }

    public Reservation Reservation { get; }
    public string Reason { get; }
}

/// <summary>Raised when a hold is cancelled.</summary>
public class ReservationCancelledEvent : BaseEvent
{
    public ReservationCancelledEvent(Reservation reservation, string? reason)
    {
        Reservation = reservation;
        Reason = reason;
    }

    public Reservation Reservation { get; }
    public string? Reason { get; }
}

/// <summary>Raised when a pending hold lapses past its ExpiresAt.</summary>
public class ReservationExpiredEvent : BaseEvent
{
    public ReservationExpiredEvent(Reservation reservation) => Reservation = reservation;
    public Reservation Reservation { get; }
}

/// <summary>Raised when a reservation becomes fully paid.</summary>
public class ReservationPaidEvent : BaseEvent
{
    public ReservationPaidEvent(Reservation reservation) => Reservation = reservation;
    public Reservation Reservation { get; }
}

/// <summary>Raised when a confirmed/paid hold is converted into a renting.</summary>
public class ReservationConvertedEvent : BaseEvent
{
    public ReservationConvertedEvent(Reservation reservation, Renting renting)
    {
        Reservation = reservation;
        Renting = renting;
    }

    public Reservation Reservation { get; }
    public Renting Renting { get; }
}
