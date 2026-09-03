namespace RemSolution.Application.Common.Exceptions;

/// <summary>What kind of record is holding the period.</summary>
public enum BookingConflictKind
{
    Renting = 1,
    Reservation = 2,
    Unavailability = 3,
}

/// <summary>
/// Thrown when a renting or reservation would overlap an existing active booking
/// for the same car (see the availability rule). Mapped to 409 Conflict with the
/// machine-readable code "booking_conflict".
/// </summary>
public class BookingConflictException : Exception
{
    public BookingConflictException(
        int carId, DateTime startDate, DateTime endDate,
        BookingConflictKind kind, int conflictingId)
        : base($"Car {carId} is already booked for a period overlapping {startDate:yyyy-MM-dd} – {endDate:yyyy-MM-dd} ({kind} {conflictingId}).")
    {
        CarId = carId;
        StartDate = startDate;
        EndDate = endDate;
        Kind = kind;
        ConflictingId = conflictingId;
    }

    public int CarId { get; }
    public DateTime StartDate { get; }
    public DateTime EndDate { get; }

    // Which record holds the period, so the agency is told what to cancel rather
    // than just that it cannot proceed.
    public BookingConflictKind Kind { get; }
    public int ConflictingId { get; }
}
