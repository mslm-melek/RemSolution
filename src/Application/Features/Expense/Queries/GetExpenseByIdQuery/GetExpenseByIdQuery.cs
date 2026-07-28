using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Expense.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Expense.Queries.GetExpenseByIdQuery
{
    [Authorize(Policy = Permissions.ExpenseRead)]
    [RequiresFeature(FeatureFlags.Expenses)]
    public record GetExpenseByIdQuery(int Id) : IRequest<ExpenseDto?>;

    public class GetExpenseByIdQueryHandler : IRequestHandler<GetExpenseByIdQuery, ExpenseDto?>
    {
        private readonly IApplicationDbContext _context;

        public GetExpenseByIdQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ExpenseDto?> Handle(GetExpenseByIdQuery request, CancellationToken cancellationToken)
        {
            return await _context.Expenses
                .AsNoTracking()
                .Where(e => e.Id == request.Id)
                .ProjectToType<ExpenseDto>()
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
