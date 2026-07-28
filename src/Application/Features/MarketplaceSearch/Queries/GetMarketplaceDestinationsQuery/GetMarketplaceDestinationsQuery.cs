using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.MarketplaceSearch.DTOs;

namespace RemSolution.Application.Features.MarketplaceSearch.Queries.GetMarketplaceDestinationsQuery
{
    // Fills the "where" half of the public search bar: the countries and places
    // that actually have cars on offer. Public and cross-agency, like the search
    // itself — no [Authorize], no [RequiresFeature], no tenant.
    public record GetMarketplaceDestinationsQuery : IRequest<IList<MarketplaceDestinationDto>>;

    public class GetMarketplaceDestinationsQueryHandler
        : IRequestHandler<GetMarketplaceDestinationsQuery, IList<MarketplaceDestinationDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetMarketplaceDestinationsQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<MarketplaceDestinationDto>> Handle(
            GetMarketplaceDestinationsQuery request, CancellationToken cancellationToken)
        {
            var offered = MarketplaceCars.Offered(_context);

            // Counted as two aggregates rather than one grouped by a conditional
            // country key: SQL Server groups plain columns, and a CASE over a
            // nullable navigation in the key is not worth the translation risk.
            var byPlace = await offered
                .Where(c => c.Branch != null)
                .GroupBy(c => new
                {
                    CountryId = c.Branch!.CountryId,
                    CountryName = c.Branch.Country!.Name,
                    BranchId = c.Branch.Id,
                    BranchName = c.Branch.Name,
                    c.AgencyId,
                    AgencyName = c.Agency!.Name
                })
                .Select(g => new { g.Key, CarCount = g.Count() })
                .ToListAsync(cancellationToken);

            // Branchless cars still belong to their agency's country.
            var byAgencyCountry = await offered
                .Where(c => c.Branch == null && c.Agency != null)
                .GroupBy(c => new { c.Agency!.CountryId, CountryName = c.Agency.Country!.Name })
                .Select(g => new { g.Key, CarCount = g.Count() })
                .ToListAsync(cancellationToken);

            var names = new Dictionary<int, string?>();
            var counts = new Dictionary<int, int>();
            var places = new Dictionary<int, List<MarketplacePlaceDto>>();

            foreach (var row in byPlace)
            {
                names[row.Key.CountryId] = row.Key.CountryName;
                counts[row.Key.CountryId] = counts.GetValueOrDefault(row.Key.CountryId) + row.CarCount;

                if (!places.TryGetValue(row.Key.CountryId, out var list))
                {
                    places[row.Key.CountryId] = list = new List<MarketplacePlaceDto>();
                }

                list.Add(new MarketplacePlaceDto
                {
                    BranchId = row.Key.BranchId,
                    Name = row.Key.BranchName,
                    AgencyId = row.Key.AgencyId,
                    AgencyName = row.Key.AgencyName,
                    CarCount = row.CarCount
                });
            }

            foreach (var row in byAgencyCountry)
            {
                names[row.Key.CountryId] = row.Key.CountryName;
                counts[row.Key.CountryId] = counts.GetValueOrDefault(row.Key.CountryId) + row.CarCount;
            }

            return counts
                .Select(country => new MarketplaceDestinationDto
                {
                    CountryId = country.Key,
                    CountryName = names[country.Key],
                    CarCount = country.Value,
                    Places = (places.GetValueOrDefault(country.Key) ?? new List<MarketplacePlaceDto>())
                        .OrderBy(p => p.Name)
                        .ToList()
                })
                .OrderBy(d => d.CountryName)
                .ToList();
        }
    }
}
