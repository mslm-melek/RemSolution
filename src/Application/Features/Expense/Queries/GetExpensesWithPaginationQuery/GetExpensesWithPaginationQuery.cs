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
        bool OnlyUnpaid = false,
        // Column the table is sorted by, named after the Angular matColumnDef;
        // anything unrecognised falls back to the most recent expense first.
        string? SortBy = null,
        bool SortDescending = true
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

            var descending = request.SortDescending;

            // Money is an optional owned type, so an amount is read through a
            // null check rather than dereferenced (EF cannot order by a null
            // owned reference).
            var ordered = request.SortBy.NormalizeSortKey() switch
            {
                "car" => query.OrderByField(e => e.Car!.Matricule, descending),
                "type" => query.OrderByField(e => e.ExpenseType!.Name, descending),
                "amount" => query.OrderByField(
                    e => e.ExpenseAmount == null ? 0m : e.ExpenseAmount.Amount, descending),
                "paid" => query.OrderByField(
                    e => e.PaidAmount == null ? 0m : e.PaidAmount.Amount, descending),
                "outstanding" => query.OrderByField(
                    e => (e.ExpenseAmount == null ? 0m : e.ExpenseAmount.Amount)
                         - (e.PaidAmount == null ? 0m : e.PaidAmount.Amount), descending),
                _ => query.OrderByField(e => e.ExpenseDate, descending),
            };

            return await ordered
                .ThenBy(e => e.Id)
                .ProjectToType<ExpenseDto>()
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}
