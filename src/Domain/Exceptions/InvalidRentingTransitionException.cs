using RemSolution.Domain.Enums;

namespace RemSolution.Domain.Exceptions;

/// <summary>
/// A renting lifecycle method was called from a state that does not permit it.
/// Mapped to 409 with the code "invalid_transition" — the same code as its
/// reservation counterpart, because the client reads both the same way.
/// </summary>
public class InvalidRentingTransitionException : Exception
{
    public InvalidRentingTransitionException(RentingState from, string action)
        : base($"A renting in state '{from}' cannot be {action}.")
    {
        From = from;
        Action = action;
    }

    public RentingState From { get; }
    public string Action { get; }
}
