using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.Features.Car.Commands.UpdateCarCommand
{
    [Authorize(Policy = Permissions.CarUpdate)]
    [RequiresFeature(FeatureFlags.Cars)]
    public record UpdateCarCommand : IRequest
    {
        public int Id { get; init; }
        // The row version the client last read; the update targets exactly that
        // version so a concurrent change surfaces as a 409 (see P.8).
        public byte[]? RowVersion { get; init; }
        public int? ModelId { get; init; }
        public int? BranchId { get; init; }
        public CarStatus Status { get; init; }
        public decimal? DailyRate { get; init; }
        public DateTime FirstCirculationDate { get; init; }
        public string? Color { get; init; }
        // The photo is owned by UploadCarPhotoCommand; see CreateCarCommand.
        public int? Power { get; init; }
        public FuelType? FuelType { get; init; }
    }

    public class UpdateCarCommandHandler : IRequestHandler<UpdateCarCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;

        public UpdateCarCommandHandler(IApplicationDbContext context, IAgencySettingsProvider settings)
        {
            _context = context;
            _settings = settings;
        }

        public async Task Handle(UpdateCarCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Cars
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            _context.SetOriginalRowVersion(entity, request.RowVersion);

            entity.ModelId = request.ModelId;
            entity.BranchId = request.BranchId;
            entity.Status = request.Status;
            entity.DailyRate = request.DailyRate is decimal rate
                ? Money.Of(rate, (await _settings.GetAsync(entity.AgencyId, cancellationToken)).CurrencyCode)
                : null;
            entity.FirstCirculationDate = request.FirstCirculationDate;
            entity.Color = request.Color;
            entity.Power = request.Power;
            entity.FuelType = request.FuelType;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

}
