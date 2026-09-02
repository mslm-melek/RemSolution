using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Car.Commands.CreateCarUnavailabilityCommand
{
    /// <summary>
    /// Declares a car off the road for a period — the garage on the 20th, the
    /// inspection next month. Editing the fleet, not the bookings, so it carries
    /// Car.Update: whoever may take a car out of service may say when.
    /// </summary>
    [Authorize(Policy = Permissions.CarUpdate)]
    [RequiresFeature(FeatureFlags.Cars)]
    [Auditable("CreateCarUnavailability", "Car")]
    public record CreateCarUnavailabilityCommand : IRequest<int>
    {
        public int CarId { get; init; }

        /// <summary>Wall-clock, as picked (see form-utils.fromDateInput).</summary>
        public DateTime StartDate { get; init; }

        /// <summary>Wall-clock and exclusive — the day the car is available again.</summary>
        public DateTime EndDate { get; init; }

        public CarUnavailabilityReason Reason { get; init; } = CarUnavailabilityReason.Maintenance;

        public string? Note { get; init; }
    }

    public class CreateCarUnavailabilityCommandHandler
        : IRequestHandler<CreateCarUnavailabilityCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAvailabilityChecker _availability;

        public CreateCarUnavailabilityCommandHandler(
            IApplicationDbContext context, IAvailabilityChecker availability)
        {
            _context = context;
            _availability = availability;
        }

        public async Task<int> Handle(
            CreateCarUnavailabilityCommand request, CancellationToken cancellationToken)
        {
            // The tenant filter makes this a check rather than a leak.
            var car = await _context.Cars
                .FirstOrDefaultAsync(c => c.Id == request.CarId, cancellationToken);

            Guard.Against.NotFound(request.CarId, car);

            var entity = CarUnavailability.Create(
                request.CarId, request.StartDate, request.EndDate, request.Reason, request.Note);

            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            // A block competes for the same dates as a booking, so it takes the
            // same lock as one (see IApplicationDbContext.AcquireCarWriteLockAsync).
            await _context.AcquireCarWriteLockAsync(request.CarId, cancellationToken);

            // Refused rather than allowed to overlap: a car cannot be at the
            // garage and out with a client on the same day, and silently
            // accepting both would leave two records disagreeing with nothing to
            // arbitrate them. The desk is told, and moves or cancels the hire.
            // Overlapping another block is refused for the same reason — two
            // blocks over one period should be one block.
            await _availability.EnsureCarAvailableAsync(
                request.CarId, request.StartDate, request.EndDate, null, null, cancellationToken);

            _context.CarUnavailabilities.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return entity.Id;
        }
    }

    public class CreateCarUnavailabilityCommandValidator
        : AbstractValidator<CreateCarUnavailabilityCommand>
    {
        public CreateCarUnavailabilityCommandValidator(ILocalizer localizer)
        {
            RuleFor(v => v.CarId).GreaterThan(0);
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
