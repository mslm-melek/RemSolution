using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.MarketplaceSearch.DTOs;
using RemSolution.Domain.Enums;

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
            return await _context.Cars
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => c.Id == request.Id
                            && !c.IsDeleted
                            && c.Status == CarStatus.Active
                            && c.DailyRate != null)
                .ProjectToType<MarketplaceCarDto>()
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
