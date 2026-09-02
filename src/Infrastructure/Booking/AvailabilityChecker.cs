using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Enums;

namespace RemSolution.Infrastructure.Booking;

// Overlap check over the tenant-scoped Rentings/Reservations/CarUnavailabilities
// sets (all three carry a global AgencyId filter, so this only ever sees the
// current agency's rows). Two half-open [start, end) periods overlap iff
// existing.Start < requested.End && requested.Start < existing.End. Terminal
// rentings (Done/Cancelled) and inactive reservations (only active holds —
// PendingConfirmation/Confirmed/Paid — block; Converted/Rejected/Expired/Cancelled
// don't) are excluded. Rows with missing dates compare as unknown in SQL and are
// therefore ignored — an incomplete booking cannot block.
//
// The three predicates go to the database as ONE round trip. Marketplace search
// runs this per candidate car of every nearby agency, so three sequential
// AnyAsync calls tripled the round trips on the one path where they are counted
// in the hundreds. Each is a covering seek on IX_*_CarId_Dates.
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
        CancellationToken cancellationToken,
        int? excludeUnavailabilityId = null)
    {
        var rentings = _context.Rentings
            .Where(r =>
                r.CarId == carId
                && r.Id != excludeRentingId
                && r.RentingState != RentingState.Done
                && r.RentingState != RentingState.Cancelled
                && r.StartDate < endDate
                && r.EndDate > startDate)
            .Select(_ => 1);

        var reservations = _context.Reservations
            .Where(r =>
                r.CarId == carId
                && r.Id != excludeReservationId
                && (r.Status == ReservationStatus.PendingConfirmation
                    || r.Status == ReservationStatus.Confirmed
                    || r.Status == ReservationStatus.Paid)
                && r.StartDate < endDate
                && r.EndDate > startDate)
            .Select(_ => 1);

        // Declared time off the road — the garage, the inspection. Blocks a
        // booking exactly as a hire does, which is the whole point of the table:
        // Car.Status only says whether the car is available today.
        var unavailabilities = _context.CarUnavailabilities
            .Where(u =>
                u.CarId == carId
                && u.Id != excludeUnavailabilityId
                && u.StartDate < endDate
                && u.EndDate > startDate)
            .Select(_ => 1);

        // Concat, not three AnyAsync: EF renders it as UNION ALL inside one
        // EXISTS, so SQL Server stops at the first row it finds.
        var conflict = await rentings
            .Concat(reservations)
            .Concat(unavailabilities)
            .AnyAsync(cancellationToken);

        if (conflict)
        {
            throw new BookingConflictException(carId, startDate, endDate);
        }
    }
}
