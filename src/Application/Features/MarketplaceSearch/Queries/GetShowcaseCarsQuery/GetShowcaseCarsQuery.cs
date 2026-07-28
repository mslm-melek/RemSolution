using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.MarketplaceSearch.DTOs;

namespace RemSolution.Application.Features.MarketplaceSearch.Queries.GetShowcaseCarsQuery
{
    // Feeds the home-page slideshow. Deliberately date-free: this is a shop
    // window, not an availability answer — picking dates is what /browse is for.
    public record GetShowcaseCarsQuery(int Count = 8) : IRequest<IList<MarketplaceCarDto>>;

    public class GetShowcaseCarsQueryHandler
        : IRequestHandler<GetShowcaseCarsQuery, IList<MarketplaceCarDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetShowcaseCarsQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<MarketplaceCarDto>> Handle(
            GetShowcaseCarsQuery request, CancellationToken cancellationToken)
        {
            return await MarketplaceCars.Offered(_context)
                // Photographed cars first — a slideshow of placeholder icons sells
                // nothing — then the most recently added.
                .OrderByDescending(c => c.Images!.Any(i => i.IsPrimary && i.MediumFileId != null)
                                        || c.PhotoFileId != null)
                .ThenByDescending(c => c.Id)
                .Take(request.Count)
                .ProjectToType<MarketplaceCarDto>()
                .ToListAsync(cancellationToken);
        }
    }

    public class GetShowcaseCarsQueryValidator : AbstractValidator<GetShowcaseCarsQuery>
    {
        public GetShowcaseCarsQueryValidator()
        {
            RuleFor(v => v.Count).InclusiveBetween(1, 24);
        }
    }
}
