using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Renting.Booking;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using FluentValidation.Results;

namespace RemSolution.Application.Features.Renting.Commands.UpdateRentingCommand
{
    // ISensitiveRequest: SecondNewClient carries identity-document numbers, so the
    // pipeline behaviours must never destructure this request into logs.
    [Authorize(Policy = Permissions.RentingUpdate)]
    [RequiresFeature(FeatureFlags.Rentings)]
    public record UpdateRentingCommand : IRequest, ISensitiveRequest
    {
        public int Id { get; init; }
        // The row version the client last read; a concurrent change surfaces as
        // a 409 (see P.8).
        public byte[]? RowVersion { get; init; }
        public int CarId { get; init; }
        public int ClientId { get; init; }

        // The second driver. At most one of the two is supplied and neither means
        // "no second driver", which is also how one is REMOVED from a booking.
        //
        // The renter is deliberately not creatable here — reassigning an existing
        // booking to a brand-new person is a different act from adding the partner
        // who turned up to share the driving, which is the case this covers.
        public int? SecondClientId { get; init; }
        public NewRentingClient? SecondNewClient { get; init; }

        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public int? StartMileage { get; init; }
        public int? EndMileage { get; init; }

        /// <summary>
        /// The agreed price for the whole period, when it is not the automatic one
        /// (see CreateRentingCommand.PriceOverride). Applied whether or not the
        /// dates moved, so a price can be corrected on its own; null leaves the
        /// snapshot alone unless the car or the period changed, in which case the
        /// period is re-quoted as before.
        /// </summary>
        public decimal? PriceOverride { get; init; }

        public string? Notes { get; init; }
    }

    public class UpdateRentingCommandHandler : IRequestHandler<UpdateRentingCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IPricingService _pricing;
        private readonly IAgencySettingsProvider _settings;
        private readonly IAvailabilityChecker _availability;
        // Only needed to create a second driver inline, which applies the Clients
        // module's own gates and quota (see RentingClients).
        private readonly IIdentityService _identityService;
        private readonly IUser _user;
        private readonly ITenantProvider _tenant;
        private readonly TimeProvider _dateTime;

        public UpdateRentingCommandHandler(
            IApplicationDbContext context, IPricingService pricing,
            IAgencySettingsProvider settings, IAvailabilityChecker availability,
            IIdentityService identityService, IUser user,
            ITenantProvider tenant, TimeProvider dateTime)
        {
            _context = context;
            _pricing = pricing;
            _settings = settings;
            _availability = availability;
            _identityService = identityService;
            _user = user;
            _tenant = tenant;
            _dateTime = dateTime;
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

            // Only the car or dates affect the automatic price. When they change it
            // is a deliberate re-quote; otherwise the original snapshot is preserved
            // (P.3 — a note/mileage/driver edit must never silently reprice from
            // the car's current DailyRate). A price typed in by the agent wins over
            // both: it is the figure that was actually agreed.
            var repricing = request.PriceOverride is null
                && (request.CarId != entity.CarId
                    || request.StartDate != entity.StartDate
                    || request.EndDate != entity.EndDate);

            if (repricing && car.DailyRate is null)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.CarId),
                        "The car has no daily rate; set a price before booking it, " +
                        "or agree a price for this renting.")
                });
            }

            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _context.AcquireTenantWriteLockAsync(cancellationToken);

            await _availability.EnsureCarAvailableAsync(
                request.CarId, request.StartDate, request.EndDate,
                excludeRentingId: entity.Id, excludeReservationId: null, cancellationToken);

            // A second driver typed in here is created inside this transaction, so a
            // booking conflict above or a concurrency conflict below takes the new
            // client row with it — no orphan person from a failed edit.
            //
            // Creating them needs a SaveChanges to get their key, and it runs BEFORE
            // the renting's own fields are assigned below. That ordering is what
            // keeps the concurrency check on the final save: at this point the
            // renting is still Unchanged (SetOriginalRowVersion only sets an
            // original value), so no UPDATE for it is flushed early and the token is
            // not spent. Pinned by SecondDriverTests.
            var renter = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == request.ClientId, cancellationToken);

            Guard.Against.NotFound(request.ClientId, renter);

            var secondDriver = await RentingClients.ResolveSecondDriverAsync(
                new RentingClientContext(_context, _user, _identityService, _tenant, _dateTime),
                renter, request.SecondClientId, request.SecondNewClient, cancellationToken);

            entity.CarId = request.CarId;
            entity.ClientId = request.ClientId;
            entity.SecondClientId = secondDriver?.Id;
            entity.StartDate = request.StartDate;
            entity.EndDate = request.EndDate;
            entity.StartMileage = request.StartMileage;
            entity.EndMileage = request.EndMileage;
            entity.Notes = request.Notes;

            if (request.PriceOverride is decimal agreed)
            {
                // Same currency rule as the create path: the car's, then the
                // agency's. Keeping the existing snapshot's currency would be
                // wrong when the edit is what moves the renting to another car.
                entity.Price = Money.Of(
                    agreed,
                    car.DailyRate?.Currency
                        ?? (await _settings.GetAsync(car.AgencyId, cancellationToken)).CurrencyCode)
                    .Round();
            }
            else if (repricing)
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
        public UpdateRentingCommandValidator(
            IApplicationDbContext context, TimeProvider dateTime, ILocalizer localizer)
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.CarId).GreaterThan(0);
            RuleFor(v => v.ClientId).GreaterThan(0);

            // At most one second-driver source; neither means the booking has none.
            RuleFor(v => v.SecondClientId)
                .Must((command, secondClientId) => secondClientId is null || command.SecondNewClient is null)
                    .WithMessage(_ => localizer["Validation.Renting.SecondDriverOneSource"]);

            RuleFor(v => v.SecondClientId)
                .GreaterThan(0).When(v => v.SecondClientId.HasValue)
                .NotEqual(v => v.ClientId).When(v => v.SecondClientId.HasValue)
                    .WithMessage(_ => localizer["Validation.Renting.SecondDriverDistinct"]);

            // Same identity rules as on create; "not the same person as the renter"
            // needs both rows and lives in the handler.
            RuleFor(v => v.SecondNewClient!)
                .SetValidator(new NewRentingClientValidator(context, dateTime, localizer))
                .When(v => v.SecondNewClient is not null);
            RuleFor(v => v.StartDate).NotEmpty();
            RuleFor(v => v.EndDate)
                .NotEmpty()
                .GreaterThan(v => v.StartDate)
                    .WithMessage(_ => localizer["Validation.Booking.EndAfterStart"]);
            RuleFor(v => v.StartMileage)
                .GreaterThanOrEqualTo(0).When(v => v.StartMileage.HasValue);
            RuleFor(v => v.EndMileage)
                .GreaterThanOrEqualTo(0).When(v => v.EndMileage.HasValue);
            RuleFor(v => v.PriceOverride)
                .GreaterThanOrEqualTo(0).When(v => v.PriceOverride.HasValue);
            RuleFor(v => v.Notes).MaximumLength(1000);
        }
    }
}
