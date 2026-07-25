using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.ExtraServicesType.Commands.UpdateExtraServicesTypeCommand
{
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

        public UpdateExtraServicesTypeCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(UpdateExtraServicesTypeCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.ExtraServicesTypes
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.Name = request.Name;
            entity.Amount = request.Amount;
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
