using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.ExtraServicesTypeTemplate.Commands.DeactivateExtraServicesTypeTemplateCommand
{
    // Retires a template: new agencies stop receiving it; existing copies stay.
    [Authorize(Roles = Roles.PlatformAdministrator)]
    [Auditable("DeactivateExtraServicesTypeTemplate", "ExtraServicesTypeTemplate")]
    public record DeactivateExtraServicesTypeTemplateCommand(int Id) : IRequest;

    public class DeactivateExtraServicesTypeTemplateCommandHandler
        : IRequestHandler<DeactivateExtraServicesTypeTemplateCommand>
    {
        private readonly IApplicationDbContext _context;

        public DeactivateExtraServicesTypeTemplateCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeactivateExtraServicesTypeTemplateCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.ExtraServicesTypeTemplates
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.IsActive = false;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
