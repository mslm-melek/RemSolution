using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using FluentValidation.Results;
using RentingEntity = RemSolution.Domain.Entities.Renting;

namespace RemSolution.Application.Features.Renting.Commands.CreateRentingCommand
{
    [Authorize(Policy = Permissions.RentingCreate)]
    [RequiresFeature(FeatureFlags.Rentings)]
    public record CreateRentingCommand : IRequest<int>
    {
        public int CarId { get; init; }
        public int ClientId { get; init; }
        public int? SecondClientId { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public int? StartMileage { get; init; }
        public string? Notes { get; init; }
    }

    public class CreateRentingCommandHandler : IRequestHandler<CreateRentingCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly IPricingService _pricing;
        private readonly IAvailabilityChecker _availability;

        public CreateRentingCommandHandler(
            IApplicationDbContext context, IPricingService pricing, IAvailabilityChecker availability)
        {
            _context = context;
            _pricing = pricing;
            _availability = availability;
        }

        public async Task<int> Handle(CreateRentingCommand request, CancellationToken cancellationToken)
        {
            var car = await _context.Cars
                .FirstOrDefaultAsync(c => c.Id == request.CarId, cancellationToken);

            Guard.Against.NotFound(request.CarId, car);

            if (car.Status != CarStatus.Active)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.CarId),
                        "The car is not Active and cannot be booked.")
                });
            }

            if (car.DailyRate is null)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.CarId),
                        "The car has no daily rate; set a price before booking it.")
                });
            }

            // Snapshot the agreed price once, at creation (see IPricingService).
            var price = _pricing.CalculateRentalPrice(car, request.StartDate, request.EndDate);

            var entity = new RentingEntity
            {
                CarId = request.CarId,
                ClientId = request.ClientId,
                SecondClientId = request.SecondClientId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                StartMileage = request.StartMileage,
                Price = price,
                RentingState = RentingState.NotYet,
                Notes = request.Notes,
            };

            // Availability check and insert are atomic under the per-agency write
            // lock: a concurrent booking cannot slip in between check and insert.
            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _context.AcquireTenantWriteLockAsync(cancellationToken);

            await _availability.EnsureCarAvailableAsync(
                request.CarId, request.StartDate, request.EndDate, null, null, cancellationToken);

            _context.Rentings.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return entity.Id;
        }
    }
}

namespace RemSolution.Application.Features.Renting.Commands.CreateRentingCommand
{
    public class CreateRentingCommandValidator : AbstractValidator<CreateRentingCommand>
    {
        public CreateRentingCommandValidator(ILocalizer localizer)
        {
            RuleFor(v => v.CarId).GreaterThan(0);
            RuleFor(v => v.ClientId).GreaterThan(0);
            RuleFor(v => v.SecondClientId)
                .GreaterThan(0).When(v => v.SecondClientId.HasValue)
                .NotEqual(v => v.ClientId).When(v => v.SecondClientId.HasValue)
                    .WithMessage(_ => localizer["Validation.Renting.SecondDriverDistinct"]);
            RuleFor(v => v.StartDate).NotEmpty();
            RuleFor(v => v.EndDate)
                .NotEmpty()
                .GreaterThan(v => v.StartDate)
                    .WithMessage(_ => localizer["Validation.Booking.EndAfterStart"]);
            RuleFor(v => v.StartMileage)
                .GreaterThanOrEqualTo(0).When(v => v.StartMileage.HasValue);
            RuleFor(v => v.Notes).MaximumLength(1000);
        }
    }
}
