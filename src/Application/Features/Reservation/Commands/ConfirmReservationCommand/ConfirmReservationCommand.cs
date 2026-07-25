using RemSolution.Application.Common.Audit;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using FluentValidation.Results;
using RentingEntity = RemSolution.Domain.Entities.Renting;

namespace RemSolution.Application.Features.Reservation.Commands.ConfirmReservationCommand
{
    // Confirms a pending hold into an actual renting: re-checks availability,
    // creates the Renting from the reservation's snapshot, links them, and marks
    // the reservation Confirmed. Returns the new renting id.
    [Authorize(Policy = Permissions.ReservationUpdate)]
    [RequiresFeature(FeatureFlags.Reservations)]
    [Auditable("ConfirmReservation", "Reservation")]
    public record ConfirmReservationCommand(int Id, byte[]? RowVersion = null) : IRequest<int>;

    public class ConfirmReservationCommandHandler : IRequestHandler<ConfirmReservationCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAvailabilityChecker _availability;

        public ConfirmReservationCommandHandler(
            IApplicationDbContext context, IAvailabilityChecker availability)
        {
            _context = context;
            _availability = availability;
        }

        public async Task<int> Handle(ConfirmReservationCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Reservations
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            if (entity.Status != ReservationStatus.Pending)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "Only a pending reservation can be confirmed.")
                });
            }

            if (entity.CarId is not int carId || entity.StartDate is not DateTime start || entity.EndDate is not DateTime end)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "The reservation is missing a car or dates and cannot be confirmed.")
                });
            }

            _context.SetOriginalRowVersion(entity, request.RowVersion);

            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _context.AcquireTenantWriteLockAsync(cancellationToken);

            // The hold itself is excluded; any OTHER active booking that appeared
            // for this car since the hold was placed blocks the confirmation.
            await _availability.EnsureCarAvailableAsync(
                carId, start, end, excludeRentingId: null, excludeReservationId: entity.Id, cancellationToken);

            var renting = new RentingEntity
            {
                CarId = carId,
                ClientId = entity.ClientId,
                StartDate = start,
                EndDate = end,
                // Keep the agreed price from the hold — not re-quoted.
                Price = entity.Price,
                RentingState = RentingState.NotYet,
                Notes = entity.Notes,
            };

            _context.Rentings.Add(renting);

            entity.Renting = renting;
            entity.Status = ReservationStatus.Confirmed;

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return renting.Id;
        }
    }
}
