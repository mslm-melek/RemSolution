using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.ExpenseType.Commands.DeactivateExpenseTypeCommand
{
    // "Delete" is deactivation: kept so historical expenses still resolve their type.
    [Authorize(Policy = Policies.AgencyOrPlatformAdmin)]
    [RequiresFeature(FeatureFlags.Expenses)]
    public record DeactivateExpenseTypeCommand(int Id) : IRequest;

    public class DeactivateExpenseTypeCommandHandler : IRequestHandler<DeactivateExpenseTypeCommand>
    {
        private readonly IApplicationDbContext _context;

        public DeactivateExpenseTypeCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeactivateExpenseTypeCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.ExpenseTypes
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.IsActive = false;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
