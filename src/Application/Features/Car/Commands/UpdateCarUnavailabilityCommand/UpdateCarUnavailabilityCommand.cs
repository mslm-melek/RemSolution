using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Car.Commands.UpdateCarUnavailabilityCommand
{
    /// <summary>
    /// Moves or re-labels a block — the garage kept the car two more days. The
    /// car itself is not editable: see <see cref="CarUnavailability.Amend"/>.
    /// </summary>
    [Authorize(Policy = Permissions.CarUpdate)]
    [RequiresFeature(FeatureFlags.Cars)]
    [Auditable("UpdateCarUnavailability", "Car")]
    public record UpdateCarUnavailabilityCommand : IRequest
    {
        public int Id { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public CarUnavailabilityReason Reason { get; init; } = CarUnavailabilityReason.Maintenance;
        public string? Note { get; init; }
    }

    public class UpdateCarUnavailabilityCommandHandler
        : IRequestHandler<UpdateCarUnavailabilityCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAvailabilityChecker _availability;

        public UpdateCarUnavailabilityCommandHandler(
            IApplicationDbContext context, IAvailabilityChecker availability)
        {
            _context = context;
            _availability = availability;
        }

        public async Task Handle(
            UpdateCarUnavailabilityCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.CarUnavailabilities
                .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _context.AcquireCarWriteLockAsync(entity.CarId, cancellationToken);

            // Excluding itself: the row being moved is in the table the check
            // reads, so without this every amend would collide with the block it
            // is amending.
            await _availability.EnsureCarAvailableAsync(
                entity.CarId, request.StartDate, request.EndDate, null, null, cancellationToken,
                excludeUnavailabilityId: entity.Id);

            entity.Amend(request.StartDate, request.EndDate, request.Reason, request.Note);

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
    }

    public class UpdateCarUnavailabilityCommandValidator
        : AbstractValidator<UpdateCarUnavailabilityCommand>
    {
        public UpdateCarUnavailabilityCommandValidator(ILocalizer localizer)
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.StartDate).NotEmpty();
            RuleFor(v => v.EndDate)
                .NotEmpty()
                .GreaterThan(v => v.StartDate)
                    .WithMessage(_ => localizer["Validation.Booking.EndAfterStart"]);
            RuleFor(v => v.Reason).IsInEnum();
            RuleFor(v => v.Note).MaximumLength(CarUnavailability.MaxNoteLength);
        }
    }
}
