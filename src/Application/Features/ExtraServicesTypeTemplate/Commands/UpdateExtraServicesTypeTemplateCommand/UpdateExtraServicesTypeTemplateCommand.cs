using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.ExtraServicesTypeTemplate.Commands.UpdateExtraServicesTypeTemplateCommand
{
    // Same rules as UpdateExpenseTypeTemplateCommand: the name reaches every copy
    // the agency has not renamed; on/off stays the agency's call.
    [Authorize(Roles = Roles.PlatformAdministrator)]
    [Auditable("UpdateExtraServicesTypeTemplate", "ExtraServicesTypeTemplate")]
    public record UpdateExtraServicesTypeTemplateCommand : IRequest
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public bool IsActive { get; init; }
    }

    public class UpdateExtraServicesTypeTemplateCommandHandler : IRequestHandler<UpdateExtraServicesTypeTemplateCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICatalogTemplateCopier _copier;

        public UpdateExtraServicesTypeTemplateCommandHandler(IApplicationDbContext context, ICatalogTemplateCopier copier)
        {
            _context = context;
            _copier = copier;
        }

        public async Task Handle(UpdateExtraServicesTypeTemplateCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.ExtraServicesTypeTemplates
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.Name = request.Name.Trim();
            entity.IsActive = request.IsActive;

            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await _copier.PropagateExtraServicesTypeAsync(entity.Id, cancellationToken);

            if (entity.IsActive)
            {
                await _copier.CopyMissingAsync(agencyId: null, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
    }

    public class UpdateExtraServicesTypeTemplateCommandValidator
        : AbstractValidator<UpdateExtraServicesTypeTemplateCommand>
    {
        public UpdateExtraServicesTypeTemplateCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        }
    }
}
