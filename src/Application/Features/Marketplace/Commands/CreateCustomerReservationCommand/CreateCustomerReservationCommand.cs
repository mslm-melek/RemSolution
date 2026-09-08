using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Notifications;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using FluentValidation.Results;
using ClientEntity = RemSolution.Domain.Entities.Client;
using ReservationEntity = RemSolution.Domain.Entities.Reservation;

namespace RemSolution.Application.Features.Marketplace.Commands.CreateCustomerReservationCommand
{
    // A signed-in customer requests to book a car from ANY agency. It creates a
    // Pending reservation hold in the CAR's agency (which the agency then
    // confirms via the normal flow). Driver details create/attach the customer's
    // Client record in that agency, linked by MarketplaceUserId.
    [Authorize(Policy = Policies.CustomerOnly)]
    public record CreateCustomerReservationCommand : IRequest<int>
    {
        public int CarId { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public DateTime BirthDate { get; init; }
    }

    public class CreateCustomerReservationCommandHandler
        : IRequestHandler<CreateCustomerReservationCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly IUser _user;
        private readonly IAgencySettingsProvider _settings;
        private readonly IPricingService _pricing;
        private readonly IAvailabilityChecker _availability;
        private readonly TimeProvider _dateTime;
        private readonly INotificationService _notifications;

        public CreateCustomerReservationCommandHandler(
            IApplicationDbContext context, IUser user, IAgencySettingsProvider settings,
            IPricingService pricing, IAvailabilityChecker availability, TimeProvider dateTime,
            INotificationService notifications)
        {
            _context = context;
            _user = user;
            _settings = settings;
            _pricing = pricing;
            _availability = availability;
            _dateTime = dateTime;
            _notifications = notifications;
        }

        public async Task<int> Handle(CreateCustomerReservationCommand request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            // The customer has no tenant, so the car is loaded cross-tenant to
            // discover which agency owns it.
            var car = await _context.Cars
                .IgnoreQueryFilters()
                .Include(c => c.Agency)
                .FirstOrDefaultAsync(c => c.Id == request.CarId && !c.IsDeleted, cancellationToken);

            Guard.Against.NotFound(request.CarId, car);

            // Repeats the agency half of MarketplaceCars.Offered: this path needs
            // a TRACKED car, so it cannot go through that queryable — and without
            // the gate an unpublished agency's car would still take bookings by
            // id. Change the rule there and change it here.
            if (car.Status != CarStatus.Active || car.DailyRate is null
                || car.Agency?.PublishedAt is null)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.CarId), "This car is not available for booking.")
                });
            }

            var price = _pricing.CalculateRentalPrice(car, request.StartDate, request.EndDate);

            // Act as the car's agency so the tenant filter, the AgencyId write
            // stamp and the per-agency write lock all target it.
            using var _ = AmbientTenant.Push(car.AgencyId);

            var settings = await _settings.GetAsync(car.AgencyId, cancellationToken);

            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            // Both locks, agency first: this path may add the customer's Client
            // row, of which there is one per agency per customer.
            await _context.AcquireTenantWriteLockAsync(cancellationToken);
            await _context.AcquireCarWriteLockAsync(request.CarId, cancellationToken);

            await _availability.EnsureCarAvailableAsync(
                request.CarId, request.StartDate, request.EndDate, null, null, cancellationToken);

            // One Client row per agency per customer, keyed by MarketplaceUserId.
            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.MarketplaceUserId == userId, cancellationToken);

            if (client is null)
            {
                client = new ClientEntity
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    BirthDate = request.BirthDate,
                    MarketplaceUserId = userId,
                };
                _context.Clients.Add(client);
            }

            var reservation = ReservationEntity.Create(
                carId: request.CarId,
                startDate: request.StartDate,
                endDate: request.EndDate,
                price: price,
                expiresAt: _dateTime.GetUtcNow().UtcDateTime.AddHours(settings.ReservationExpiryHours));
            reservation.Client = client;

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            // The request the agency has to answer. Raised inside the ambient
            // tenant pushed above — notifications are agency-scoped — and after
            // the commit, so a mail failure cannot undo the booking.
            var modelName = await _context.ModelCars
                .AsNoTracking()
                .Where(m => m.Id == car.ModelId)
                .Select(m => m.Name)
                .FirstOrDefaultAsync(cancellationToken);

            await ReservationAlerts.RaiseRequestedAsync(
                _notifications, reservation.Id, client.Id,
                client.FirstName, client.LastName,
                car.Matricule, modelName,
                request.StartDate, cancellationToken);

            return reservation.Id;
        }
    }
}

namespace RemSolution.Application.Features.Marketplace.Commands.CreateCustomerReservationCommand
{
    public class CreateCustomerReservationCommandValidator : AbstractValidator<CreateCustomerReservationCommand>
    {
        public CreateCustomerReservationCommandValidator(ILocalizer localizer)
        {
            RuleFor(v => v.CarId).GreaterThan(0);
            RuleFor(v => v.StartDate).NotEmpty();
            RuleFor(v => v.EndDate)
                .NotEmpty()
                .GreaterThan(v => v.StartDate)
                    .WithMessage(_ => localizer["Validation.Booking.ReturnAfterPickup"]);
            RuleFor(v => v.FirstName).NotEmpty().MaximumLength(200);
            RuleFor(v => v.LastName).NotEmpty().MaximumLength(200);
            RuleFor(v => v.BirthDate)
                .NotEmpty()
                .LessThan(v => DateTime.UtcNow).WithMessage(_ => localizer["Validation.Client.BirthDateInPast"]);
        }
    }
}
