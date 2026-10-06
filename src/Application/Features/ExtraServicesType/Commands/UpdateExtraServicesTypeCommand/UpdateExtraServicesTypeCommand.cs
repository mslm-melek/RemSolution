using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.Features.ExtraServicesType.Commands.UpdateExtraServicesTypeCommand
{
    // Renaming a standard type makes it the agency's own version; setting its
    // price does not, since the template has none (ExtraServicesType.Rename).
    [Authorize(Policy = Policies.AgencyOrPlatformAdmin)]
    [RequiresFeature(FeatureFlags.ExtraServices)]
    public record UpdateExtraServicesTypeCommand : IRequest
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public decimal? Amount { get; init; }
        public bool IsActive { get; init; }
    }

    public class UpdateExtraServicesTypeCommandHandler : IRequestHandler<UpdateExtraServicesTypeCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAgencySettingsProvider _settings;

        public UpdateExtraServicesTypeCommandHandler(IApplicationDbContext context, IAgencySettingsProvider settings)
        {
            _context = context;
            _settings = settings;
        }

        public async Task Handle(UpdateExtraServicesTypeCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.ExtraServicesTypes
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            var settings = await _settings.GetAsync(entity.AgencyId, cancellationToken);

            entity.Rename(request.Name.Trim());
            entity.Amount = request.Amount is decimal amount ? Money.Of(amount, settings.CurrencyCode) : null;
            entity.IsActive = request.IsActive;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

namespace RemSolution.Application.Features.ExtraServicesType.Commands.UpdateExtraServicesTypeCommand
{
    public class UpdateExtraServicesTypeCommandValidator : AbstractValidator<UpdateExtraServicesTypeCommand>
    {
        public UpdateExtraServicesTypeCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
            RuleFor(v => v.Amount).GreaterThanOrEqualTo(0).When(v => v.Amount.HasValue);
        }
    }
}
