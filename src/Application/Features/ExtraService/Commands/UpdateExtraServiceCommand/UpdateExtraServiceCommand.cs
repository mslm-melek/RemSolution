using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.Features.ExtraService.Commands.UpdateExtraServiceCommand
{
    [Authorize(Policy = Permissions.ExtraServiceUpdate)]
    [RequiresFeature(FeatureFlags.ExtraServices)]
    public record UpdateExtraServiceCommand : IRequest
    {
        public int Id { get; init; }
        public int ExtraServicesTypeId { get; init; }
        public decimal Amount { get; init; }
    }

    public class UpdateExtraServiceCommandHandler : IRequestHandler<UpdateExtraServiceCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;

        public UpdateExtraServiceCommandHandler(IApplicationDbContext context, IAgencySettingsProvider settings)
        {
            _context = context;
            _settings = settings;
        }

        public async Task Handle(UpdateExtraServiceCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.ExtraServices
                .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            var type = await _context.ExtraServicesTypes
                .FirstOrDefaultAsync(t => t.Id == request.ExtraServicesTypeId, cancellationToken);

            Guard.Against.NotFound(request.ExtraServicesTypeId, type);

            var settings = await _settings.GetAsync(entity.AgencyId, cancellationToken);

            entity.ExtraServicesTypeId = type.Id;
            entity.TotalAmount = Money.Of(request.Amount, settings.CurrencyCode);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

namespace RemSolution.Application.Features.ExtraService.Commands.UpdateExtraServiceCommand
{
    public class UpdateExtraServiceCommandValidator : AbstractValidator<UpdateExtraServiceCommand>
    {
        public UpdateExtraServiceCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.ExtraServicesTypeId).GreaterThan(0);
            RuleFor(v => v.Amount).GreaterThanOrEqualTo(0);
        }
    }
}
