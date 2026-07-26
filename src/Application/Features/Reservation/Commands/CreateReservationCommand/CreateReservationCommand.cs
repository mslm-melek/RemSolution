using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using FluentValidation.Results;
using ReservationEntity = RemSolution.Domain.Entities.Reservation;

namespace RemSolution.Application.Features.Reservation.Commands.CreateReservationCommand
{
    [Authorize(Policy = Permissions.ReservationCreate)]
    [RequiresFeature(FeatureFlags.Reservations)]
    public record CreateReservationCommand : IRequest<int>
    {
        public int CarId { get; init; }
        public int ClientId { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        // Optional refundable deposit held for the vehicle (agency currency).
        // Money actually collected is recorded as Payments, never hand-set here.
        public decimal? DepositAmount { get; init; }
        public string? Notes { get; init; }
    }

    public class CreateReservationCommandHandler : IRequestHandler<CreateReservationCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;
        private readonly IPricingService _pricing;
        private readonly IAvailabilityChecker _availability;
        private readonly TimeProvider _dateTime;

        public CreateReservationCommandHandler(
            IApplicationDbContext context, IAgencySettingsProvider settings,
            IPricingService pricing, IAvailabilityChecker availability, TimeProvider dateTime)
        {
            _context = context;
            _settings = settings;
            _pricing = pricing;
            _availability = availability;
            _dateTime = dateTime;
        }

        public async Task<int> Handle(CreateReservationCommand request, CancellationToken cancellationToken)
        {
            var car = await _context.Cars
                .FirstOrDefaultAsync(c => c.Id == request.CarId, cancellationToken);

            Guard.Against.NotFound(request.CarId, car);

            if (car.Status != CarStatus.Active)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.CarId),
                        "The car is not Active and cannot be reserved.")
                });
            }

            if (car.DailyRate is null)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.CarId),
                        "The car has no daily rate; set a price before reserving it.")
                });
            }

            var settings = await _settings.GetAsync(car.AgencyId, cancellationToken);

            var price = _pricing.CalculateRentalPrice(car, request.StartDate, request.EndDate);

            var entity = ReservationEntity.Create(
                carId: request.CarId,
                startDate: request.StartDate,
                endDate: request.EndDate,
                price: price,
                expiresAt: _dateTime.GetUtcNow().UtcDateTime.AddHours(settings.ReservationExpiryHours),
                clientId: request.ClientId,
                depositAmount: request.DepositAmount is decimal deposit
                    ? Money.Of(deposit, settings.CurrencyCode)
                    : null,
                notes: request.Notes);

            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _context.AcquireTenantWriteLockAsync(cancellationToken);

            await _availability.EnsureCarAvailableAsync(
                request.CarId, request.StartDate, request.EndDate, null, null, cancellationToken);

            _context.Reservations.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return entity.Id;
        }
    }
}

namespace RemSolution.Application.Features.Reservation.Commands.CreateReservationCommand
{
    public class CreateReservationCommandValidator : AbstractValidator<CreateReservationCommand>
    {
        public CreateReservationCommandValidator()
        {
            RuleFor(v => v.CarId).GreaterThan(0);
            RuleFor(v => v.ClientId).GreaterThan(0);
            RuleFor(v => v.StartDate).NotEmpty();
            RuleFor(v => v.EndDate)
                .NotEmpty()
                .GreaterThan(v => v.StartDate)
                    .WithMessage("The end date must be after the start date.");
            RuleFor(v => v.DepositAmount)
                .GreaterThanOrEqualTo(0).When(v => v.DepositAmount.HasValue);
            RuleFor(v => v.Notes).MaximumLength(1000);
        }
    }
}
