using RemSolution.Application.Common.Audit;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;
using FluentValidation.Results;

namespace RemSolution.Application.Features.Reservation.Commands.CancelReservationCommand
{
    // "Delete" for a reservation is a cancellation (never a physical delete).
    // Allowed while the hold is still active (Pending/Confirmed/Paid) — the
    // aggregate's Cancel() guards the state. On top of that we enforce the
    // agency's cancellation window (P.3.5): a hold can't be cancelled once it is
    // within CancellationWindowHours of its start. Bound to ReservationDelete.
    [Authorize(Policy = Permissions.ReservationDelete)]
    [RequiresFeature(FeatureFlags.Reservations)]
    [Auditable("CancelReservation", "Reservation")]
    public record CancelReservationCommand(int Id, string? Reason = null, byte[]? RowVersion = null) : IRequest;

    public class CancelReservationCommandHandler : IRequestHandler<CancelReservationCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;
        private readonly TimeProvider _dateTime;

        public CancelReservationCommandHandler(
            IApplicationDbContext context, IAgencySettingsProvider settings, TimeProvider dateTime)
        {
            _context = context;
            _settings = settings;
            _dateTime = dateTime;
        }

        public async Task Handle(CancelReservationCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Reservations
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            var settings = await _settings.GetAsync(entity.AgencyId, cancellationToken);

            // Too late to cancel once we are inside the cancellation window before
            // the start. A hold with no start date is always cancellable.
            if (entity.StartDate is DateTime start)
            {
                var cutoff = start.AddHours(-settings.CancellationWindowHours);
                if (_dateTime.GetUtcNow().UtcDateTime >= cutoff)
                {
                    throw new ValidationException(new[]
                    {
                        new ValidationFailure(nameof(request.Id),
                            $"This reservation can no longer be cancelled — it is within " +
                            $"{settings.CancellationWindowHours}h of its start.")
                    });
                }
            }

            _context.SetOriginalRowVersion(entity, request.RowVersion);

            // Throws InvalidReservationTransitionException (→ 409) if the hold is
            // already converted/rejected/expired/cancelled.
            entity.Cancel(request.Reason);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

namespace RemSolution.Application.Features.Reservation.Commands.CancelReservationCommand
{
    public class CancelReservationCommandValidator : AbstractValidator<CancelReservationCommand>
    {
        public CancelReservationCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.Reason).MaximumLength(1000);
        }
    }
}
