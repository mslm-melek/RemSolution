using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Reservation.Commands.ConfirmReservationCommand
{
    // Agency approves a pending hold. Under the Phase 3 lifecycle this only moves
    // the reservation to Confirmed — it does NOT create the renting; that is a
    // separate Convert step (allowed from Confirmed/Paid). The state guard lives
    // in the Reservation aggregate (Confirm()).
    //
    // Confirmation is also where the car is actually claimed: a pending request
    // blocks nothing, so this is the first moment two requests for the same days
    // can be told apart. The availability check therefore lives here and not only
    // at creation, and the agency has to reject or cancel whatever holds the
    // period before it can confirm this one.
    [Authorize(Policy = Permissions.ReservationUpdate)]
    [RequiresFeature(FeatureFlags.Reservations)]
    [Auditable("ConfirmReservation", "Reservation")]
    public record ConfirmReservationCommand(int Id, byte[]? RowVersion = null) : IRequest;

    public class ConfirmReservationCommandHandler : IRequestHandler<ConfirmReservationCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAvailabilityChecker _availability;

        public ConfirmReservationCommandHandler(
            IApplicationDbContext context, IAvailabilityChecker availability)
        {
            _context = context;
            _availability = availability;
        }

        public async Task Handle(ConfirmReservationCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Reservations
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            _context.SetOriginalRowVersion(entity, request.RowVersion);

            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            if (entity.CarId is int carId
                && entity.StartDate is DateTime start
                && entity.EndDate is DateTime end)
            {
                // Same lock the create paths take, so a concurrent booking cannot
                // slip in between this check and the status write.
                await _context.AcquireCarWriteLockAsync(carId, cancellationToken);

                await _availability.EnsureCarAvailableAsync(
                    carId, start, end, null, entity.Id, cancellationToken);
            }

            // Throws InvalidReservationTransitionException (→ 409) if not pending.
            entity.Confirm();

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
    }
}
