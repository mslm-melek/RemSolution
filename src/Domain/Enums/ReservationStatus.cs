namespace RemSolution.Domain.Enums;

/// <summary>
/// Lifecycle of a reservation — a hold on a specific car for a date range. A new
/// reservation is <see cref="Pending"/> and carries an <c>ExpiresAt</c>; a
/// background sweep flips stale pending holds to <see cref="Expired"/>.
/// Confirming a pending hold creates the renting and moves it to
/// <see cref="Confirmed"/>. <see cref="Cancelled"/> is the "delete" outcome.
/// Only <see cref="Pending"/> and <see cref="Confirmed"/> holds block a car's
/// availability.
/// </summary>
public enum ReservationStatus
{
    Pending = 0,
    Confirmed = 1,
    Cancelled = 2,
    Expired = 3,
}
