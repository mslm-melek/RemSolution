using RemSolution.Application.Common.Audit;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using FluentValidation.Results;

namespace RemSolution.Application.Features.Reservation.Commands.CancelReservationCommand
{
    // "Delete" for a reservation is a cancellation (never a physical delete).
    // Only a pending hold is cancellable here; a confirmed hold has become a
    // renting, so cancel the renting instead. Bound to ReservationDelete.
    [Authorize(Policy = Permissions.ReservationDelete)]
    [RequiresFeature(FeatureFlags.Reservations)]
    [Auditable("CancelReservation", "Reservation")]
    public record CancelReservationCommand(int Id, byte[]? RowVersion = null) : IRequest;

    public class CancelReservationCommandHandler : IRequestHandler<CancelReservationCommand>
    {
        private readonly IApplicationDbContext _context;

        public CancelReservationCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(CancelReservationCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Reservations
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            if (entity.Status == ReservationStatus.Confirmed)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "This reservation was confirmed into a renting; cancel the renting instead.")
                });
            }

            if (entity.Status != ReservationStatus.Pending)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "Only a pending reservation can be cancelled.")
                });
            }

            _context.SetOriginalRowVersion(entity, request.RowVersion);

            entity.Status = ReservationStatus.Cancelled;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
