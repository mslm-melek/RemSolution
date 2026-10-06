using FluentValidation.Results;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;

namespace RemSolution.Application.Features.ExpenseType.Commands.ResetExpenseTypeCommand
{
    // Undoes an agency's changes to a standard type: the platform's version
    // comes back and its later edits reach the copy again (ExpenseType.ResetTo).
    [Authorize(Policy = Policies.AgencyOrPlatformAdmin)]
    [RequiresFeature(FeatureFlags.Expenses)]
    public record ResetExpenseTypeCommand(int Id) : IRequest;

    public class ResetExpenseTypeCommandHandler : IRequestHandler<ResetExpenseTypeCommand>
    {
        private readonly IApplicationDbContext _context;

        public ResetExpenseTypeCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(ResetExpenseTypeCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.ExpenseTypes
                .Include(t => t.Template)
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            if (entity.Template is null)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.Id),
                        "This type is the agency's own: there is no standard version to go back to.")
                });
            }

            entity.ResetTo(entity.Template);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
