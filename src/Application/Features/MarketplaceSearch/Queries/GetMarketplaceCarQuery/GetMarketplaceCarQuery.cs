using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.MarketplaceSearch.DTOs;

namespace RemSolution.Application.Features.MarketplaceSearch.Queries.GetMarketplaceCarQuery
{
    // Public detail lookup for the booking page, cross-agency.
    public record GetMarketplaceCarQuery(int Id) : IRequest<MarketplaceCarDto?>;

    public class GetMarketplaceCarQueryHandler : IRequestHandler<GetMarketplaceCarQuery, MarketplaceCarDto?>
    {
        private readonly IApplicationDbContext _context;

        public GetMarketplaceCarQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<MarketplaceCarDto?> Handle(GetMarketplaceCarQuery request, CancellationToken cancellationToken)
        {
            // Through Offered rather than repeating its predicate: a car reachable
            // by id but absent from the search is exactly the hole a duplicated
            // rule leaves — an unpublished agency's page, linked to directly.
            return await MarketplaceCars.Offered(_context)
                .Where(c => c.Id == request.Id)
                .ProjectToType<MarketplaceCarDto>()
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
