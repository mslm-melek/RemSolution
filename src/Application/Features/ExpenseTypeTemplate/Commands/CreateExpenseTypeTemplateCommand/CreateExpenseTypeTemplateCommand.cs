using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using ExpenseTypeTemplateEntity = RemSolution.Domain.Entities.ExpenseTypeTemplate;

namespace RemSolution.Application.Features.ExpenseTypeTemplate.Commands.CreateExpenseTypeTemplateCommand
{
    // A new standard expense type: every agency gets its own copy at once, and
    // every agency created later gets one with it.
    [Authorize(Roles = Roles.PlatformAdministrator)]
    [Auditable("CreateExpenseTypeTemplate", "ExpenseTypeTemplate")]
    public record CreateExpenseTypeTemplateCommand : IRequest<int>
    {
        public string Name { get; init; } = string.Empty;
        public bool WithNotif { get; init; }
        public int? AfterKilometer { get; init; }
        public int? AfterMonth { get; init; }
    }

    public class CreateExpenseTypeTemplateCommandHandler : IRequestHandler<CreateExpenseTypeTemplateCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICatalogTemplateCopier _copier;

        public CreateExpenseTypeTemplateCommandHandler(IApplicationDbContext context, ICatalogTemplateCopier copier)
        {
            _context = context;
            _copier = copier;
        }

        public async Task<int> Handle(CreateExpenseTypeTemplateCommand request, CancellationToken cancellationToken)
        {
            var entity = new ExpenseTypeTemplateEntity
            {
                Name = request.Name.Trim(),
                WithNotif = request.WithNotif,
                AfterKilometer = request.AfterKilometer,
                AfterMonth = request.AfterMonth,
                IsActive = true,
            };

            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);

            _context.ExpenseTypeTemplates.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            await _copier.CopyMissingAsync(agencyId: null, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return entity.Id;
        }
    }

    public class CreateExpenseTypeTemplateCommandValidator : AbstractValidator<CreateExpenseTypeTemplateCommand>
    {
        public CreateExpenseTypeTemplateCommandValidator()
        {
            RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
            RuleFor(v => v.AfterKilometer).GreaterThan(0).When(v => v.AfterKilometer.HasValue);
            RuleFor(v => v.AfterMonth).GreaterThan(0).When(v => v.AfterMonth.HasValue);
        }
    }
}
