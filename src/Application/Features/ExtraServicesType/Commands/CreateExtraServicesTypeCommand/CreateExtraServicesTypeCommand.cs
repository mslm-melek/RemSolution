using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using ExtraServicesTypeEntity = RemSolution.Domain.Entities.ExtraServicesType;

namespace RemSolution.Application.Features.ExtraServicesType.Commands.CreateExtraServicesTypeCommand
{
    // Global reference catalog: only an agency or platform administrator manages
    // it (regular staff merely select types when adding an extra service). The
    // feature gate means an agency admin only reaches it when their agency has
    // ExtraServices enabled; the platform admin has no tenant so it passes.
    [Authorize(Policy = Policies.AgencyOrPlatformAdmin)]
    [RequiresFeature(FeatureFlags.ExtraServices)]
    public record CreateExtraServicesTypeCommand : IRequest<int>
    {
        public string Name { get; init; } = string.Empty;
        public decimal? Amount { get; init; }
    }

    public class CreateExtraServicesTypeCommandHandler : IRequestHandler<CreateExtraServicesTypeCommand, int>
    {
        private readonly IApplicationDbContext _context;

        public CreateExtraServicesTypeCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> Handle(CreateExtraServicesTypeCommand request, CancellationToken cancellationToken)
        {
            var entity = new ExtraServicesTypeEntity
            {
                Name = request.Name,
                Amount = request.Amount,
                IsActive = true,
            };

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
