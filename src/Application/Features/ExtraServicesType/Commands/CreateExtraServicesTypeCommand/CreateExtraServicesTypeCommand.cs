using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;
using RemSolution.Domain.ValueObjects;
using ExtraServicesTypeEntity = RemSolution.Domain.Entities.ExtraServicesType;

namespace RemSolution.Application.Features.ExtraServicesType.Commands.CreateExtraServicesTypeCommand
{
    // The agency's own catalog: only its administrator manages it (or the
    // platform administrator inside the agency's workspace); staff merely pick a
    // type when adding an extra service. The platform-wide entries are
    // ExtraServicesTypeTemplates, managed through their own commands.
    [Authorize(Policy = Policies.AgencyOrPlatformAdmin)]
    [RequiresFeature(FeatureFlags.ExtraServices)]
    public record CreateExtraServicesTypeCommand : IRequest<int>
    {
        public string Name { get; init; } = string.Empty;
        // In the agency's currency, like every amount a client sends.
        public decimal? Amount { get; init; }
    }

    public class CreateExtraServicesTypeCommandHandler : IRequestHandler<CreateExtraServicesTypeCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;
        private readonly IAgencySettingsProvider _settings;

        public CreateExtraServicesTypeCommandHandler(
            IApplicationDbContext context, ITenantProvider tenant, IAgencySettingsProvider settings)
        {
            _context = context;
            _tenant = tenant;
            _settings = settings;
        }

        public async Task<int> Handle(CreateExtraServicesTypeCommand request, CancellationToken cancellationToken)
        {
            // A platform administrator outside any workspace has no agency to add to.
            if (_tenant.AgencyId is not int agencyId)
            {
                throw new ForbiddenAccessException();
            }

            var settings = await _settings.GetAsync(agencyId, cancellationToken);

            var entity = new ExtraServicesTypeEntity
            {
                IsActive = true,
                Amount = request.Amount is decimal amount ? Money.Of(amount, settings.CurrencyCode) : null,
            };
            entity.Rename(request.Name.Trim());

            _context.ExtraServicesTypes.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }
}

namespace RemSolution.Application.Features.ExtraServicesType.Commands.CreateExtraServicesTypeCommand
{
    public class CreateExtraServicesTypeCommandValidator : AbstractValidator<CreateExtraServicesTypeCommand>
    {
        public CreateExtraServicesTypeCommandValidator()
        {
            RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
            RuleFor(v => v.Amount).GreaterThanOrEqualTo(0).When(v => v.Amount.HasValue);
        }
    }
}
