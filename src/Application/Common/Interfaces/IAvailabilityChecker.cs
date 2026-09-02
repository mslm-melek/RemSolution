namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// Enforces the car-availability rule shared by the renting and reservation
/// create/confirm flows: a car may not have two bookings whose half-open
/// <c>[start, end)</c> periods overlap. Rentings in a terminal state
/// (<see cref="Domain.Enums.RentingState.Done"/> /
/// <see cref="Domain.Enums.RentingState.Cancelled"/>) and reservations that are
/// cancelled or expired do not block. Callers run this inside the per-car
/// write lock so a concurrent booking cannot slip in between the check and the
/// insert.
/// <para>
/// A declared <see cref="Domain.Entities.CarUnavailability"/> blocks on the same
/// terms: to the calendar, a week at the garage is a week the car is taken.
/// </para>
/// </summary>
public interface IAvailabilityChecker
{
    /// <summary>
    /// Throws <see cref="Common.Exceptions.BookingConflictException"/> if the car
    /// already has an active renting, an active reservation, or declared time off
    /// the road overlapping
    /// <c>[<paramref name="startDate"/>, <paramref name="endDate"/>)</c>. Pass the
    /// id of the booking being edited/confirmed to exclude it from the check.
    /// </summary>
    Task EnsureCarAvailableAsync(
        int carId,
        DateTime startDate,
        DateTime endDate,
        int? excludeRentingId,
        int? excludeReservationId,
        CancellationToken cancellationToken,
        // Only the block-editing path passes this: amending a block would
        // otherwise collide with itself.
        int? excludeUnavailabilityId = null);
}
