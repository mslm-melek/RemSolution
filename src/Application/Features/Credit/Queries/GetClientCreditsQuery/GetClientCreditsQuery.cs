using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Credit.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Credit.Queries.GetClientCreditsQuery
{
    // The receivable side of the credits screen: per client, charged vs paid.
    // Gated on the Credits feature and Credit.Read — deliberately NOT on
    // Payment.Read, so an agency can hand someone the debt overview without
    // giving them the payment ledger itself.
    [Authorize(Policy = Permissions.CreditRead)]
    [RequiresFeature(FeatureFlags.Credits)]
    public record GetClientCreditsQuery(
        int PageNumber = 1,
        int PageSize = 10,
        // Default view is the working set: only clients who actually owe money.
        bool OnlyOutstanding = true,
        string? Search = null,
        // Column the table is sorted by, named after the Angular matColumnDef;
        // anything unrecognised falls back to the biggest debt first.
        string? SortBy = null,
        bool SortDescending = true
    ) : IRequest<PaginatedList<ClientCreditDto>>;

    public class GetClientCreditsQueryHandler
        : IRequestHandler<GetClientCreditsQuery, PaginatedList<ClientCreditDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;
        private readonly IAgencySettingsProvider _settings;

        public GetClientCreditsQueryHandler(
            IApplicationDbContext context, ITenantProvider tenant, IAgencySettingsProvider settings)
        {
            _context = context;
            _tenant = tenant;
            _settings = settings;
        }

        public async Task<PaginatedList<ClientCreditDto>> Handle(
            GetClientCreditsQuery request, CancellationToken cancellationToken)
        {
            // Amounts are all in the agency's single currency (see Money), so the
            // sums are computed as bare decimals in SQL and re-labelled once here.
            var agencyId = _tenant.AgencyId ?? throw new UnauthorizedAccessException();
            var currency = (await _settings.GetAsync(agencyId, cancellationToken)).CurrencyCode;

            var clients = _context.Clients.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var term = request.Search.Trim();
                clients = clients.Where(c =>
                    (c.FirstName != null && c.FirstName.Contains(term))
                    || (c.LastName != null && c.LastName.Contains(term))
                    || (c.CIN != null && c.CIN.Contains(term)));
            }

            // The receivable arithmetic itself is shared (see ClientCreditRows) so
            // this list and the per-client lookups cannot disagree.
            var rows = clients.ToCreditRows();

            if (request.OnlyOutstanding)
            {
                rows = rows.Where(x => x.Charged - x.Paid > 0m);
            }

            var descending = request.SortDescending;

            var ordered = request.SortBy.NormalizeSortKey() switch
            {
                "name" => rows.OrderByField(x => x.LastName, descending)
                              .ThenByField(x => x.FirstName, descending),
                "cin" => rows.OrderByField(x => x.CIN, descending),
                "openrentings" => rows.OrderByField(x => x.OpenRentingCount, descending),
                "charged" => rows.OrderByField(x => x.Charged, descending),
                "paid" => rows.OrderByField(x => x.Paid, descending),
                _ => rows.OrderByField(x => x.Charged - x.Paid, descending),
            };

            // Priced into Money once the page is in memory: only the sums need to
            // happen in SQL, and the currency is a constant per agency.
            var page = await ordered
                .ThenBy(x => x.ClientId)
                .PaginatedListAsync(request.PageNumber, request.PageSize);

            return new PaginatedList<ClientCreditDto>(
                page.Items.Select(row => row.ToDto(currency)).ToList(),
                page.TotalCount,
                page.PageNumber,
                request.PageSize);
        }
    }
}
