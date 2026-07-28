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
        int? CarId = null
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

            if (request.OnlyOutstanding)
            {
                query = query.Where(e => e.ExpenseAmount != null
                    && e.ExpenseAmount.Amount > (e.PaidAmount == null ? 0m : e.PaidAmount.Amount));
            }

            return await query
                .OrderByDescending(e => e.ExpenseAmount == null
                    ? 0m
                    : e.ExpenseAmount.Amount - (e.PaidAmount == null ? 0m : e.PaidAmount.Amount))
                .ThenByDescending(e => e.ExpenseDate)
                .Select(e => new ExpenseCreditDto
                {
                    ExpenseId = e.Id,
                    CarId = e.CarId,
                    CarMatricule = e.Car != null ? e.Car.Matricule : null,
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
                })
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}
