using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Enums;

namespace RemSolution.Infrastructure.Booking;

// Overlap check over the tenant-scoped Rentings/Reservations sets (both carry a
// global AgencyId filter, so this only ever sees the current agency's bookings).
// Two half-open [start, end) periods overlap iff existing.Start < requested.End
// && requested.Start < existing.End. Terminal rentings (Done/Cancelled) and
// inactive reservations (Cancelled/Expired) are excluded. Rows with missing
// dates compare as unknown in SQL and are therefore ignored — an incomplete
// booking cannot block.
public sealed class AvailabilityChecker : IAvailabilityChecker
{
    private readonly IApplicationDbContext _context;

    public AvailabilityChecker(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task EnsureCarAvailableAsync(
        int carId,
        DateTime startDate,
        DateTime endDate,
        int? excludeRentingId,
        int? excludeReservationId,
        CancellationToken cancellationToken)
    {
        var rentingConflict = await _context.Rentings.AnyAsync(r =>
            r.CarId == carId
            && r.Id != excludeRentingId
            && r.RentingState != RentingState.Done
            && r.RentingState != RentingState.Cancelled
            && r.StartDate < endDate
            && r.EndDate > startDate,
            cancellationToken);

        if (rentingConflict)
        {
            throw new BookingConflictException(carId, startDate, endDate);
        }

        var reservationConflict = await _context.Reservations.AnyAsync(r =>
            r.CarId == carId
            && r.Id != excludeReservationId
            && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed)
            && r.StartDate < endDate
            && r.EndDate > startDate,
            cancellationToken);

        if (reservationConflict)
        {
            throw new BookingConflictException(carId, startDate, endDate);
        }
    }
}
