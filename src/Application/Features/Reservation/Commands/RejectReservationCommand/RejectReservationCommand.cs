using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Reservation.Commands.RejectReservationCommand
{
    // Agency declines a pending hold. A reason is required — the client is shown
    // why (RejectedReason). Only a pending hold can be rejected; the guard and
    // reason enforcement live in the Reservation aggregate (Reject()).
    [Authorize(Policy = Permissions.ReservationUpdate)]
    [RequiresFeature(FeatureFlags.Reservations)]
    [Auditable("RejectReservation", "Reservation")]
    public record RejectReservationCommand(int Id, string Reason, byte[]? RowVersion = null) : IRequest;

    public class RejectReservationCommandHandler : IRequestHandler<RejectReservationCommand>
    {
        private readonly IApplicationDbContext _context;

        public RejectReservationCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(RejectReservationCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Reservations
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            _context.SetOriginalRowVersion(entity, request.RowVersion);

            entity.Reject(request.Reason);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

namespace RemSolution.Application.Features.Reservation.Commands.RejectReservationCommand
{
    public class RejectReservationCommandValidator : AbstractValidator<RejectReservationCommand>
    {
        public RejectReservationCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.Reason).NotEmpty().MaximumLength(1000);
        }
    }
}
