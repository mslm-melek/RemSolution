using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Expense.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Expense.Queries.GetExpensesWithPaginationQuery
{
    [Authorize(Policy = Permissions.ExpenseRead)]
    [RequiresFeature(FeatureFlags.Expenses)]
    public record GetExpensesWithPaginationQuery(
        int PageNumber = 1,
        int PageSize = 10,
        int? CarId = null,
        int? ExpenseTypeId = null,
        DateTime? From = null,
        DateTime? To = null,
        // Only expenses the agency still owes money on — the working set of the
        // expense side of the credits screen.
        bool OnlyUnpaid = false
    ) : IRequest<PaginatedList<ExpenseDto>>;

    public class GetExpensesWithPaginationQueryHandler
        : IRequestHandler<GetExpensesWithPaginationQuery, PaginatedList<ExpenseDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetExpensesWithPaginationQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedList<ExpenseDto>> Handle(
            GetExpensesWithPaginationQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Expenses.AsNoTracking().AsQueryable();

            if (request.CarId.HasValue)
                query = query.Where(e => e.CarId == request.CarId);

            if (request.ExpenseTypeId.HasValue)
                query = query.Where(e => e.ExpenseTypeId == request.ExpenseTypeId);

            if (request.From.HasValue)
                query = query.Where(e => e.ExpenseDate >= request.From);

            if (request.To.HasValue)
                query = query.Where(e => e.ExpenseDate <= request.To);

            if (request.OnlyUnpaid)
            {
                query = query.Where(e => e.ExpenseAmount != null
                    && e.ExpenseAmount.Amount > (e.PaidAmount == null ? 0m : e.PaidAmount.Amount));
            }

            return await query
                .OrderByDescending(e => e.ExpenseDate)
                .ProjectToType<ExpenseDto>()
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}
