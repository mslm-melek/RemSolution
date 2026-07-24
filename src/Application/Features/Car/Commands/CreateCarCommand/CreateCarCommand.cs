using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Common.Subscriptions;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.Features.Car.Commands.CreateCarCommand
{
    [Authorize(Policy = Permissions.CarCreate)]
    [RequiresFeature(FeatureFlags.Cars)]
    public record CreateCarCommand : IRequest<int>
    {
        // AgencyId is not accepted from the client: TenantEntityInterceptor
        // stamps it from the current tenant on insert.
        public string Matricule { get; init; } = string.Empty;
        public int? ModelId { get; init; }
        public int? BranchId { get; init; }
        // Omitted defaults to Active (a new car is bookable unless stated otherwise).
        public CarStatus Status { get; init; } = CarStatus.Active;
        public decimal? DailyRate { get; init; }
        public DateTime FirstCirculationDate { get; init; }
        public string? Color { get; init; }
        // The photo is deliberately absent: it is owned by UploadCarPhotoCommand,
        // which manages the StoredFile lifecycle. Accepting a URL here would let
        // callers plant arbitrary URLs.
        public int? Power { get; init; }
        public FuelType? FuelType { get; init; }
    }
    public class CreateCarCommandHandler : IRequestHandler<CreateCarCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;
        private readonly IAgencySettingsProvider _settings;
        private readonly TimeProvider _dateTime;

        public CreateCarCommandHandler(
            IApplicationDbContext context, ITenantProvider tenant,
            IAgencySettingsProvider settings, TimeProvider dateTime)
        {
            _context = context;
            _tenant = tenant;
            _settings = settings;
            _dateTime = dateTime;
        }

        public async Task<int> Handle(CreateCarCommand request, CancellationToken cancellationToken)
        {
            var entity = new RemSolution.Domain.Entities.Car
            {
                Matricule = request.Matricule,
                ModelId = request.ModelId,
                BranchId = request.BranchId,
                Status = request.Status,
                FirstCirculationDate= request.FirstCirculationDate,
                Color = request.Color,
                Power = request.Power,
                FuelType = request.FuelType
            };

            // DailyRate is denominated in the agency's currency (the client
            // sends only the amount); an unpriced car simply has no rate.
            if (request.DailyRate is decimal rate && _tenant.AgencyId is int agencyId)
            {
                var currency = (await _settings.GetAsync(agencyId, cancellationToken)).CurrencyCode;
                entity.DailyRate = Money.Of(rate, currency);
            }

            // Quota check and insert are atomic under the per-agency write
            // lock; disposing without commit rolls back.
            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _context.AcquireTenantWriteLockAsync(cancellationToken);

            await SubscriptionGuard.EnsureWithinPlanLimitAsync(
                _context, _tenant, _dateTime, _context.Cars, p => p.MaxCars, "cars", cancellationToken);

            _context.Cars.Add(entity);

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return entity.Id;
        }
    }
}
