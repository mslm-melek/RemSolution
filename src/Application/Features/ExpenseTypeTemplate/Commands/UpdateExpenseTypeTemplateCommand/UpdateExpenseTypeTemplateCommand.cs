using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.ExpenseTypeTemplate.Commands.UpdateExpenseTypeTemplateCommand
{
    // The edit reaches every agency copy the agency has not changed itself
    // (ExpenseType.IsCustomized). Whether a copy is switched on stays the
    // agency's call: retiring a template only stops new agencies receiving it,
    // and bringing it back gives it to any agency that never had it.
    [Authorize(Roles = Roles.PlatformAdministrator)]
    [Auditable("UpdateExpenseTypeTemplate", "ExpenseTypeTemplate")]
    public record UpdateExpenseTypeTemplateCommand : IRequest
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public bool WithNotif { get; init; }
        public int? AfterKilometer { get; init; }
        public int? AfterMonth { get; init; }
    }

    public class UpdateExpenseTypeTemplateCommandHandler : IRequestHandler<UpdateExpenseTypeTemplateCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICatalogTemplateCopier _copier;

        public UpdateExpenseTypeTemplateCommandHandler(IApplicationDbContext context, ICatalogTemplateCopier copier)
        {
            _context = context;
            _copier = copier;
        }

        public async Task Handle(UpdateExpenseTypeTemplateCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.ExpenseTypeTemplates
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.Name = request.Name.Trim();
            entity.WithNotif = request.WithNotif;
            entity.AfterKilometer = request.AfterKilometer;
            entity.AfterMonth = request.AfterMonth;
            entity.IsActive = request.IsActive;

            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await _copier.PropagateExpenseTypeAsync(entity.Id, cancellationToken);

            if (entity.IsActive)
            {
                await _copier.CopyMissingAsync(agencyId: null, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
    }

    public class UpdateExpenseTypeTemplateCommandValidator : AbstractValidator<UpdateExpenseTypeTemplateCommand>
    {
        public UpdateExpenseTypeTemplateCommandValidator()
        {
            RuleFor(v => v.Id).GreaterThan(0);
            RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
            RuleFor(v => v.AfterKilometer).GreaterThan(0).When(v => v.AfterKilometer.HasValue);
            RuleFor(v => v.AfterMonth).GreaterThan(0).When(v => v.AfterMonth.HasValue);
        }
    }
}
