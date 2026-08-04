using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Credit.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Credit.Queries.GetCreditsSummaryQuery
{
    // Both sides of the agency's credit position in one call, so the screen shows
    // totals over ALL rows rather than over the current page.
    [Authorize(Policy = Permissions.CreditRead)]
    [RequiresFeature(FeatureFlags.Credits)]
    public record GetCreditsSummaryQuery : IRequest<CreditsSummaryDto>;

    public class GetCreditsSummaryQueryHandler : IRequestHandler<GetCreditsSummaryQuery, CreditsSummaryDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;
        private readonly IAgencySettingsProvider _settings;

        public GetCreditsSummaryQueryHandler(
            IApplicationDbContext context, ITenantProvider tenant, IAgencySettingsProvider settings)
        {
            _context = context;
            _tenant = tenant;
            _settings = settings;
        }

        public async Task<CreditsSummaryDto> Handle(
            GetCreditsSummaryQuery request, CancellationToken cancellationToken)
        {
            var agencyId = _tenant.AgencyId ?? throw new UnauthorizedAccessException();
            var currency = (await _settings.GetAsync(agencyId, cancellationToken)).CurrencyCode;

            // Charged/paid per client from the one shared projection (see
            // ClientCreditRows), which is what the rows of the credits list are
            // built from — so these totals cannot disagree with them.
            var clientRows = _context.Clients
                .AsNoTracking()
                .ToCreditRows();

            var clientsCharged = await clientRows.SumAsync(x => x.Charged, cancellationToken);
            var clientsPaid = await clientRows.SumAsync(x => x.Paid, cancellationToken);

            // Only clients actually in debt: a client in credit (overpaid) must
            // not net off someone else's arrears.
            var debtors = clientRows.Where(x => x.Charged - x.Paid > 0m);
            var clientsOutstanding = await debtors.SumAsync(x => x.Charged - x.Paid, cancellationToken);
            var clientsInDebtCount = await debtors.CountAsync(cancellationToken);

            var expenses = _context.Expenses.AsNoTracking().Where(e => e.ExpenseAmount != null);

            var expensesTotal = await expenses.SumAsync(e => e.ExpenseAmount!.Amount, cancellationToken);
            var expensesPaid = await expenses.SumAsync(
                e => e.PaidAmount == null ? 0m : e.PaidAmount.Amount, cancellationToken);

            var unpaid = expenses.Where(e =>
                e.ExpenseAmount!.Amount > (e.PaidAmount == null ? 0m : e.PaidAmount.Amount));

            var expensesOutstanding = await unpaid.SumAsync(
                e => e.ExpenseAmount!.Amount - (e.PaidAmount == null ? 0m : e.PaidAmount.Amount),
                cancellationToken);
            var unpaidExpenseCount = await unpaid.CountAsync(cancellationToken);

            return new CreditsSummaryDto
            {
                Currency = currency,
                ClientsCharged = new MoneyDto(clientsCharged, currency),
                ClientsPaid = new MoneyDto(clientsPaid, currency),
                ClientsOutstanding = new MoneyDto(clientsOutstanding, currency),
                ClientsInDebtCount = clientsInDebtCount,
                ExpensesTotal = new MoneyDto(expensesTotal, currency),
                ExpensesPaid = new MoneyDto(expensesPaid, currency),
                ExpensesOutstanding = new MoneyDto(expensesOutstanding, currency),
                UnpaidExpenseCount = unpaidExpenseCount,
                Net = new MoneyDto(clientsOutstanding - expensesOutstanding, currency),
            };
        }
    }
}
