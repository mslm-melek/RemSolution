using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using FluentValidation.Results;

namespace RemSolution.Application.Features.Renting.Commands.UpdateRentingCommand
{
    [Authorize(Policy = Permissions.RentingUpdate)]
    [RequiresFeature(FeatureFlags.Rentings)]
    public record UpdateRentingCommand : IRequest
    {
        public int Id { get; init; }
        // The row version the client last read; a concurrent change surfaces as
        // a 409 (see P.8).
        public byte[]? RowVersion { get; init; }
        public int CarId { get; init; }
        public int ClientId { get; init; }
        public int? SecondClientId { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public int? StartMileage { get; init; }
        public int? EndMileage { get; init; }
        public string? Notes { get; init; }
    }

    public class UpdateRentingCommandHandler : IRequestHandler<UpdateRentingCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IPricingService _pricing;
        private readonly IAvailabilityChecker _availability;

        public UpdateRentingCommandHandler(
            IApplicationDbContext context, IPricingService pricing, IAvailabilityChecker availability)
        {
            _context = context;
            _pricing = pricing;
            _availability = availability;
        }

        public async Task Handle(UpdateRentingCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Rentings
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            if (entity.RentingState is RentingState.Done or RentingState.Cancelled)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "A completed or cancelled renting can no longer be edited.")
                });
            }

            _context.SetOriginalRowVersion(entity, request.RowVersion);

            var car = await _context.Cars
                .FirstOrDefaultAsync(c => c.Id == request.CarId, cancellationToken);

            Guard.Against.NotFound(request.CarId, car);

            // Changing to a non-Active car is rejected; keeping the existing car
            // (e.g. one since put in maintenance) does not block a correction.
            if (request.CarId != entity.CarId && car.Status != CarStatus.Active)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.CarId),
                        "The car is not Active and cannot be booked.")
                });
            }

            // Only the car or dates affect the price. When they change it is a
            // deliberate re-quote; otherwise the original snapshot is preserved
            // (P.3 — a note/mileage/driver edit must never silently reprice from
            // the car's current DailyRate).
            var repricing = request.CarId != entity.CarId
                || request.StartDate != entity.StartDate
                || request.EndDate != entity.EndDate;

            if (repricing && car.DailyRate is null)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.CarId),
                        "The car has no daily rate; set a price before booking it.")
                });
            }

            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _context.AcquireTenantWriteLockAsync(cancellationToken);

            await _availability.EnsureCarAvailableAsync(
                request.CarId, request.StartDate, request.EndDate,
                excludeRentingId: entity.Id, excludeReservationId: null, cancellationToken);

            entity.CarId = request.CarId;
            entity.ClientId = request.ClientId;
            entity.SecondClientId = request.SecondClientId;
            entity.StartDate = request.StartDate;
            entity.EndDate = request.EndDate;
            entity.StartMileage = request.StartMileage;
            entity.EndMileage = request.EndMileage;
            entity.Notes = request.Notes;

            if (repricing)
            {
                entity.Price = _pricing.CalculateRentalPrice(car, request.StartDate, request.EndDate);
            }

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
    }
}

namespace RemSolution.Application.Features.Renting.Commands.UpdateRentingCommand
{
    public class UpdateRentingCommandValidator : AbstractValidator<UpdateRentingCommand>
    {
        public UpdateRentingCommandValidator(ILocalizer localizer)
        {
            RuleFor(v => v.Id).GreaterThan(0);
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
            RuleFor(v => v.EndMileage)
                .GreaterThanOrEqualTo(0).When(v => v.EndMileage.HasValue);
            RuleFor(v => v.Notes).MaximumLength(1000);
        }
    }
}
