using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Subscriptions;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

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
        private readonly TimeProvider _dateTime;

        public CreateCarCommandHandler(IApplicationDbContext context, ITenantProvider tenant, TimeProvider dateTime)
        {
            _context = context;
            _tenant = tenant;
            _dateTime = dateTime;
        }

        public async Task<int> Handle(CreateCarCommand request, CancellationToken cancellationToken)
        {
            var entity = new RemSolution.Domain.Entities.Car
            {
                Matricule = request.Matricule,
                ModelId = request.ModelId,
                FirstCirculationDate= request.FirstCirculationDate,
                Color = request.Color,
                Power = request.Power,
                FuelType = request.FuelType
            };

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
