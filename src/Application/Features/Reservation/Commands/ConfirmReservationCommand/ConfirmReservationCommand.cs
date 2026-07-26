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
    [Authorize(Policy = Permissions.ReservationUpdate)]
    [RequiresFeature(FeatureFlags.Reservations)]
    [Auditable("ConfirmReservation", "Reservation")]
    public record ConfirmReservationCommand(int Id, byte[]? RowVersion = null) : IRequest;

    public class ConfirmReservationCommandHandler : IRequestHandler<ConfirmReservationCommand>
    {
        private readonly IApplicationDbContext _context;

        public ConfirmReservationCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(ConfirmReservationCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Reservations
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            _context.SetOriginalRowVersion(entity, request.RowVersion);

            // Throws InvalidReservationTransitionException (→ 409) if not pending.
            entity.Confirm();

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
