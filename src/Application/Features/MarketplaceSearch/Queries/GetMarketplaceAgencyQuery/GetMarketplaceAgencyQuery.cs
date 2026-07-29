using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Features.MarketplaceSearch.DTOs;
using RemSolution.Domain.Entities;

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
                .GroupBy(c => new
                {
                    BranchId = c.Branch!.Id,
                    BranchName = c.Branch.Name,
                    // Geography is (longitude, latitude): X long, Y lat.
                    Latitude = c.Branch.Location != null ? (double?)c.Branch.Location.Y : null,
                    Longitude = c.Branch.Location != null ? (double?)c.Branch.Location.X : null,
                })
                .Select(g => new { g.Key, CarCount = g.Count() })
                .ToListAsync(cancellationToken);

            // One grouped pass over the agency's reviews gives the average, the
            // count and the star breakdown; the page needs all three, and reviews
            // are platform-level so there is no filter to bypass here.
            var byStar = await _context.AgencyReviews
                .AsNoTracking()
                .Where(r => r.AgencyId == request.Id)
                .GroupBy(r => r.Rating)
                .Select(g => new { Rating = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var reviewCount = byStar.Sum(s => s.Count);

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
                Rating = new AgencyRatingSummaryDto
                {
                    ReviewCount = reviewCount,
                    // Null, not 0: an agency nobody has rated yet is unrated, and
                    // the page says so instead of showing it as the worst on the
                    // marketplace.
                    AverageRating = reviewCount == 0
                        ? null
                        : (double)byStar.Sum(s => s.Rating * s.Count) / reviewCount,
                    // Always five slots, one star … five stars, so the bars line
                    // up whether or not a rating was ever given.
                    Counts = Enumerable
                        .Range(AgencyReview.MinRating, AgencyReview.MaxRating - AgencyReview.MinRating + 1)
                        .Select(star => byStar.FirstOrDefault(s => s.Rating == star)?.Count ?? 0)
                        .ToList(),
                },
                Places = places
                    .Select(p => new MarketplacePlaceDto
                    {
                        BranchId = p.Key.BranchId,
                        Name = p.Key.BranchName,
                        AgencyId = agency.Id,
                        AgencyName = agency.Name,
                        CarCount = p.CarCount,
                        Latitude = p.Key.Latitude,
                        Longitude = p.Key.Longitude,
                    })
                    .OrderBy(p => p.Name)
                    .ToList()
            };
        }
    }
}
