using RemSolution.Domain.Enums;

namespace RemSolution.Domain.Exceptions;

/// <summary>
/// Thrown by the <see cref="Entities.Reservation"/> aggregate when a lifecycle
/// method is called from a state that does not permit it (e.g. confirming a hold
/// that is already cancelled). Mapped to 409 Conflict with the machine-readable
/// code "invalid_transition".
/// </summary>
public class InvalidReservationTransitionException : Exception
{
    public InvalidReservationTransitionException(ReservationStatus from, string action)
        : base($"A reservation in state '{from}' cannot be {action}.")
    {
        From = from;
        Action = action;
    }

    public ReservationStatus From { get; }
    public string Action { get; }
}
