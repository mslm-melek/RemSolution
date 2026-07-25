using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.ExtraServicesType.Commands.DeactivateExtraServicesTypeCommand
{
    // "Delete" for an add-on type is deactivation (P.11): it is hidden from new
    // pickers but kept so historical extra services still resolve their type.
    [Authorize(Policy = Policies.AgencyOrPlatformAdmin)]
    [RequiresFeature(FeatureFlags.ExtraServices)]
    public record DeactivateExtraServicesTypeCommand(int Id) : IRequest;

    public class DeactivateExtraServicesTypeCommandHandler : IRequestHandler<DeactivateExtraServicesTypeCommand>
    {
        private readonly IApplicationDbContext _context;

        public DeactivateExtraServicesTypeCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeactivateExtraServicesTypeCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.ExtraServicesTypes
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.IsActive = false;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
