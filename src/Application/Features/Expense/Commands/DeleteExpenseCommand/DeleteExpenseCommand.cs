using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Expense.Commands.DeleteExpenseCommand
{
    // An expense is the agency's own cost record, not a client-facing ledger
    // entry like Payment, so a mis-keyed one is physically removed (same rule as
    // ExtraService). Nothing points at an Expense, so there is no orphan to fix
    // up; the attached facture file, once that flow exists, is cleaned by the
    // StoredFile orphan sweep.
    [Authorize(Policy = Permissions.ExpenseDelete)]
    [RequiresFeature(FeatureFlags.Expenses)]
    public record DeleteExpenseCommand(int Id) : IRequest;

    public class DeleteExpenseCommandHandler : IRequestHandler<DeleteExpenseCommand>
    {
        private readonly IApplicationDbContext _context;

        public DeleteExpenseCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeleteExpenseCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Expenses
                .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            _context.Expenses.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
