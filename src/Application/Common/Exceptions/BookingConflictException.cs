namespace RemSolution.Application.Common.Exceptions;

/// <summary>
/// Thrown when a renting or reservation would overlap an existing active booking
/// for the same car (see the availability rule). Mapped to 409 Conflict with the
/// machine-readable code "booking_conflict".
/// </summary>
public class BookingConflictException : Exception
{
    public BookingConflictException(int carId, DateTime startDate, DateTime endDate)
        : base($"Car {carId} is already booked for a period overlapping {startDate:yyyy-MM-dd} – {endDate:yyyy-MM-dd}.")
    {
        CarId = carId;
        StartDate = startDate;
        EndDate = endDate;
    }

    public int CarId { get; }
    public DateTime StartDate { get; }
    public DateTime EndDate { get; }
}
