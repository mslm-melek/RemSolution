using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using FluentValidation.Results;

namespace RemSolution.Application.Features.Reservation.Commands.UpdateReservationCommand
{
    [Authorize(Policy = Permissions.ReservationUpdate)]
    [RequiresFeature(FeatureFlags.Reservations)]
    public record UpdateReservationCommand : IRequest
    {
        public int Id { get; init; }
        public byte[]? RowVersion { get; init; }
        public int CarId { get; init; }
        public int ClientId { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public decimal? PayedPrice { get; init; }
        public string? Notes { get; init; }
    }

    public class UpdateReservationCommandHandler : IRequestHandler<UpdateReservationCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;
        private readonly IPricingService _pricing;
        private readonly IAvailabilityChecker _availability;

        public UpdateReservationCommandHandler(
            IApplicationDbContext context, IAgencySettingsProvider settings,
            IPricingService pricing, IAvailabilityChecker availability)
        {
            _context = context;
            _settings = settings;
            _pricing = pricing;
            _availability = availability;
        }

        public async Task Handle(UpdateReservationCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Reservations
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            if (entity.Status != ReservationStatus.Pending)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "Only a pending reservation can be edited.")
                });
            }

            _context.SetOriginalRowVersion(entity, request.RowVersion);

            var car = await _context.Cars
                .FirstOrDefaultAsync(c => c.Id == request.CarId, cancellationToken);

            Guard.Against.NotFound(request.CarId, car);

            if (request.CarId != entity.CarId && car.Status != CarStatus.Active)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.CarId),
                        "The car is not Active and cannot be reserved.")
                });
            }

            // Car/dates drive the price; re-quote only when they change so a
            // notes/deposit edit preserves the held price (P.3).
            var repricing = request.CarId != entity.CarId
                || request.StartDate != entity.StartDate
                || request.EndDate != entity.EndDate;

            if (repricing && car.DailyRate is null)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.CarId),
                        "The car has no daily rate; set a price before reserving it.")
                });
            }

            var settings = await _settings.GetAsync(entity.AgencyId, cancellationToken);

            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _context.AcquireTenantWriteLockAsync(cancellationToken);

            await _availability.EnsureCarAvailableAsync(
                request.CarId, request.StartDate, request.EndDate,
                excludeRentingId: null, excludeReservationId: entity.Id, cancellationToken);

            entity.CarId = request.CarId;
            entity.ClientId = request.ClientId;
            entity.StartDate = request.StartDate;
            entity.EndDate = request.EndDate;
            if (repricing)
            {
                entity.Price = _pricing.CalculateRentalPrice(car, request.StartDate, request.EndDate);
            }
            entity.PayedPrice = request.PayedPrice is decimal paid
                ? Money.Of(paid, settings.CurrencyCode)
                : null;
            entity.Notes = request.Notes;

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
    }
}

namespace RemSolution.Application.Features.Reservation.Commands.UpdateReservationCommand
{
    public class UpdateReservationCommandValidator : AbstractValidator<UpdateReservationCommand>
    {
        public UpdateReservationCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.CarId).GreaterThan(0);
            RuleFor(v => v.ClientId).GreaterThan(0);
            RuleFor(v => v.StartDate).NotEmpty();
            RuleFor(v => v.EndDate)
                .NotEmpty()
                .GreaterThan(v => v.StartDate)
                    .WithMessage("The end date must be after the start date.");
            RuleFor(v => v.PayedPrice)
                .GreaterThanOrEqualTo(0).When(v => v.PayedPrice.HasValue);
            RuleFor(v => v.Notes).MaximumLength(1000);
        }
    }
}
