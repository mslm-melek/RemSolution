using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Features.MarketplaceSearch.DTOs;

namespace RemSolution.Application.Features.MarketplaceSearch.Queries.GetMarketplaceAgencyQuery
{
    // The public shopfront header for one agency. Its cars are NOT returned here:
    // the page lists them with SearchAvailableCarsQuery(agencyId: …) so the
    // availability rule stays in one place and the dates stay changeable.
    public record GetMarketplaceAgencyQuery(int Id) : IRequest<MarketplaceAgencyDto?>;

    public class GetMarketplaceAgencyQueryHandler
        : IRequestHandler<GetMarketplaceAgencyQuery, MarketplaceAgencyDto?>
    {
        private readonly IApplicationDbContext _context;

        public GetMarketplaceAgencyQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<MarketplaceAgencyDto?> Handle(
            GetMarketplaceAgencyQuery request, CancellationToken cancellationToken)
        {
            // Agency is platform-level (not ITenantEntity), so no filter bypass is
            // needed to read one as an anonymous visitor.
            var agency = await _context.Agencies
                .AsNoTracking()
                .Where(a => a.Id == request.Id)
                .Select(a => new
                {
                    a.Id,
                    a.Name,
                    a.Address,
                    a.PhoneNumber,
                    a.Email,
                    CountryName = a.Country != null ? a.Country.Name : null
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (agency is null)
            {
                return null;
            }

            var offered = MarketplaceCars.Offered(_context).Where(c => c.AgencyId == request.Id);

            var carCount = await offered.CountAsync(cancellationToken);

            // The cheapest offer, read as a row so the rate keeps its currency.
            var cheapest = await offered
                .OrderBy(c => c.DailyRate!.Amount)
                .Select(c => new { c.DailyRate!.Amount, c.DailyRate!.Currency })
                .FirstOrDefaultAsync(cancellationToken);

            var places = await offered
                .Where(c => c.Branch != null)
                .GroupBy(c => new { BranchId = c.Branch!.Id, BranchName = c.Branch.Name })
                .Select(g => new { g.Key, CarCount = g.Count() })
                .ToListAsync(cancellationToken);

            return new MarketplaceAgencyDto
            {
                Id = agency.Id,
                Name = agency.Name,
                Address = agency.Address,
                PhoneNumber = agency.PhoneNumber,
                Email = agency.Email,
                CountryName = agency.CountryName,
                CarCount = carCount,
                FromDailyRate = cheapest is null ? null : new MoneyDto(cheapest.Amount, cheapest.Currency),
                Places = places
                    .Select(p => new MarketplacePlaceDto
                    {
                        BranchId = p.Key.BranchId,
                        Name = p.Key.BranchName,
                        AgencyId = agency.Id,
                        AgencyName = agency.Name,
                        CarCount = p.CarCount
                    })
                    .OrderBy(p => p.Name)
                    .ToList()
            };
        }
    }
}
