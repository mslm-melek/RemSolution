using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Credit.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Credit.Queries.GetExpenseCreditsQuery
{
    // The payable side of the credits screen: per expense, amount vs settled.
    // Gated on Credits/Credit.Read like its client-side twin, so the overview is
    // grantable without the Expenses module itself.
    [Authorize(Policy = Permissions.CreditRead)]
    [RequiresFeature(FeatureFlags.Credits)]
    public record GetExpenseCreditsQuery(
        int PageNumber = 1,
        int PageSize = 10,
        // Default view is the working set: only expenses still owing.
        bool OnlyOutstanding = true,
        int? CarId = null,
        // Same car/type narrowing the standalone expense list used to offer, kept
        // when the payable tab took over from it.
        int? ExpenseTypeId = null,
        // Column the table is sorted by, named after the Angular matColumnDef;
        // anything unrecognised falls back to the biggest debt first.
        string? SortBy = null,
        bool SortDescending = true
    ) : IRequest<PaginatedList<ExpenseCreditDto>>;

    public class GetExpenseCreditsQueryHandler
        : IRequestHandler<GetExpenseCreditsQuery, PaginatedList<ExpenseCreditDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetExpenseCreditsQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedList<ExpenseCreditDto>> Handle(
            GetExpenseCreditsQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Expenses.AsNoTracking().AsQueryable();

            if (request.CarId.HasValue)
                query = query.Where(e => e.CarId == request.CarId);

            if (request.ExpenseTypeId.HasValue)
                query = query.Where(e => e.ExpenseTypeId == request.ExpenseTypeId);

            if (request.OnlyOutstanding)
            {
                query = query.Where(e => e.ExpenseAmount != null
                    && e.ExpenseAmount.Amount > (e.PaidAmount == null ? 0m : e.PaidAmount.Amount));
            }

            var descending = request.SortDescending;

            var ordered = request.SortBy.NormalizeSortKey() switch
            {
                "date" => query.OrderByField(e => e.ExpenseDate, descending),
                "car" => query.OrderByField(e => e.Car!.Matricule, descending),
                "type" => query.OrderByField(e => e.ExpenseType!.Name, descending),
                "amount" => query.OrderByField(
                    e => e.ExpenseAmount == null ? 0m : e.ExpenseAmount.Amount, descending),
                "paid" => query.OrderByField(
                    e => e.PaidAmount == null ? 0m : e.PaidAmount.Amount, descending),
                _ => query.OrderByField(
                    e => (e.ExpenseAmount == null ? 0m : e.ExpenseAmount.Amount)
                         - (e.PaidAmount == null ? 0m : e.PaidAmount.Amount), descending),
            };

            return await ordered
                .ThenByDescending(e => e.ExpenseDate)
                .Select(e => new ExpenseCreditDto
                {
                    ExpenseId = e.Id,
                    CarId = e.CarId,
                    CarMatricule = e.Car != null ? e.Car.Matricule : null,
                    ExpenseTypeId = e.ExpenseTypeId,
                    ExpenseTypeName = e.ExpenseType != null ? e.ExpenseType.Name : null,
                    ExpenseDate = e.ExpenseDate,
                    Amount = e.ExpenseAmount == null
                        ? null
                        : new MoneyDto(e.ExpenseAmount.Amount, e.ExpenseAmount.Currency),
                    Paid = e.PaidAmount == null
                        ? null
                        : new MoneyDto(e.PaidAmount.Amount, e.PaidAmount.Currency),
                    Outstanding = e.ExpenseAmount == null
                        ? null
                        : new MoneyDto(
                            e.ExpenseAmount.Amount - (e.PaidAmount == null ? 0m : e.PaidAmount.Amount),
                            e.ExpenseAmount.Currency),
                    Description = e.Description,
                    FactureFileUrl = e.FactureFile != null ? e.FactureFile.Url : null,
                    FactureFileName = e.FactureFile != null ? e.FactureFile.OriginalFileName : null,
                })
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}
