namespace RemSolution.Domain.Enums;

/// <summary>
/// Lifecycle of a renting. A new renting starts <see cref="NotYet"/> (upcoming);
/// picking the car up moves it to <see cref="InProgress"/>; returning it to
/// <see cref="Done"/>. <see cref="Cancelled"/> is the "delete" outcome — a
/// renting is never physically removed (financial record), only cancelled.
/// <see cref="Done"/> and <see cref="Cancelled"/> are terminal and do not block
/// a car's availability.
/// </summary>
public enum RentingState
{
    Done = 0,
    InProgress = 1,
    NotYet = 2,
    Cancelled = 3,
}

