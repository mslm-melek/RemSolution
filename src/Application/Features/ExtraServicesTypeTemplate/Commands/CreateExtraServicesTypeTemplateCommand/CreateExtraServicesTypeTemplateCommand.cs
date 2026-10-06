using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using ExtraServicesTypeTemplateEntity = RemSolution.Domain.Entities.ExtraServicesTypeTemplate;

namespace RemSolution.Application.Features.ExtraServicesTypeTemplate.Commands.CreateExtraServicesTypeTemplateCommand
{
    // A new standard add-on: every agency gets its own copy at once, priceless
    // until the agency sets a price in its own currency.
    [Authorize(Roles = Roles.PlatformAdministrator)]
    [Auditable("CreateExtraServicesTypeTemplate", "ExtraServicesTypeTemplate")]
    public record CreateExtraServicesTypeTemplateCommand : IRequest<int>
    {
        public string Name { get; init; } = string.Empty;
    }

    public class CreateExtraServicesTypeTemplateCommandHandler
        : IRequestHandler<CreateExtraServicesTypeTemplateCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICatalogTemplateCopier _copier;

        public CreateExtraServicesTypeTemplateCommandHandler(IApplicationDbContext context, ICatalogTemplateCopier copier)
        {
            _context = context;
            _copier = copier;
        }

        public async Task<int> Handle(CreateExtraServicesTypeTemplateCommand request, CancellationToken cancellationToken)
        {
            var entity = new ExtraServicesTypeTemplateEntity
            {
                Name = request.Name.Trim(),
                IsActive = true,
            };

            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            _context.ExtraServicesTypeTemplates.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            await _copier.CopyMissingAsync(agencyId: null, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return entity.Id;
        }
    }

    public class CreateExtraServicesTypeTemplateCommandValidator
        : AbstractValidator<CreateExtraServicesTypeTemplateCommand>
    {
        public CreateExtraServicesTypeTemplateCommandValidator()
        {
            RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        }
    }
}
