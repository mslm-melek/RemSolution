using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.ExpenseTypeTemplate.Commands.DeactivateExpenseTypeTemplateCommand
{
    // Retires a template: new agencies stop receiving it. Existing copies are
    // left alone — an agency may have years of expenses on one.
    [Authorize(Roles = Roles.PlatformAdministrator)]
    [Auditable("DeactivateExpenseTypeTemplate", "ExpenseTypeTemplate")]
    public record DeactivateExpenseTypeTemplateCommand(int Id) : IRequest;

    public class DeactivateExpenseTypeTemplateCommandHandler : IRequestHandler<DeactivateExpenseTypeTemplateCommand>
    {
        private readonly IApplicationDbContext _context;

        public DeactivateExpenseTypeTemplateCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeactivateExpenseTypeTemplateCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.ExpenseTypeTemplates
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.IsActive = false;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
