using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Credit.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Credit.Queries.GetClientCreditsByIdsQuery
{
    // The credits list answers "who owes us money", ordered by debt. A client
    // list answers a different question — these clients, in their own order — and
    // still wants the debt beside each name. Rather than let the client screen
    // compute money of its own (which would put a receivable outside Credit.Read),
    // it asks here for the page it is showing, in one round-trip.
    //
    // Clients with nothing owed are returned too, at zero: a caller matching rows
    // by id needs to tell "settled" from "not in the answer".
    [Authorize(Policy = Permissions.CreditRead)]
    [RequiresFeature(FeatureFlags.Credits)]
    public record GetClientCreditsByIdsQuery(int[]? ClientIds = null)
        : IRequest<IList<ClientCreditDto>>;

    public class GetClientCreditsByIdsQueryHandler
        : IRequestHandler<GetClientCreditsByIdsQuery, IList<ClientCreditDto>>
    {
        // A page of a list is the unit this serves; a caller asking for far more
        // than that wants the credits list itself, which pages properly.
        private const int MaxIds = 200;

        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;
        private readonly IAgencySettingsProvider _settings;

        public GetClientCreditsByIdsQueryHandler(
            IApplicationDbContext context, ITenantProvider tenant, IAgencySettingsProvider settings)
        {
            _context = context;
            _tenant = tenant;
            _settings = settings;
        }

        public async Task<IList<ClientCreditDto>> Handle(
            GetClientCreditsByIdsQuery request, CancellationToken cancellationToken)
        {
            var ids = (request.ClientIds ?? Array.Empty<int>()).Distinct().Take(MaxIds).ToArray();

            if (ids.Length == 0)
                return new List<ClientCreditDto>();

            var agencyId = _tenant.AgencyId ?? throw new UnauthorizedAccessException();
            var currency = (await _settings.GetAsync(agencyId, cancellationToken)).CurrencyCode;

            // The tenant filter still applies, so ids belonging to another agency
            // simply do not come back.
            var rows = await _context.Clients
                .AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .ToCreditRows()
                .ToListAsync(cancellationToken);

            return rows.Select(row => row.ToDto(currency)).ToList();
        }
    }
}
