using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Features.MarketplaceSearch.DTOs;

namespace RemSolution.Application.Features.MarketplaceSearch.Queries.GetAgencyReviewsQuery
{
    // The reviews under an agency's public shopfront. Anonymous, like the rest of
    // the shop window — no [Authorize], no [RequiresFeature], no tenant. Reviews
    // are platform-level rows (see AgencyReview), so this reads them straight,
    // with no query-filter bypass.
    public record GetAgencyReviewsQuery(
        int AgencyId,
        int PageNumber = 1,
        int PageSize = 10
    ) : IRequest<PaginatedList<AgencyReviewDto>>;

    public class GetAgencyReviewsQueryHandler
        : IRequestHandler<GetAgencyReviewsQuery, PaginatedList<AgencyReviewDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetAgencyReviewsQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedList<AgencyReviewDto>> Handle(
            GetAgencyReviewsQuery request, CancellationToken cancellationToken)
        {
            return await _context.AgencyReviews
                .AsNoTracking()
                .Where(r => r.AgencyId == request.AgencyId)
                .OrderByDescending(r => r.SubmittedAt)
                .ThenByDescending(r => r.Id)
                .Select(r => new AgencyReviewDto
                {
                    Id = r.Id,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    AuthorName = r.AuthorName,
                    SubmittedAt = r.SubmittedAt,
                    // Read off the review row, never through Renting → Car: those
                    // are tenant-filtered and would be empty for a visitor.
                    CarName = r.CarName,
                })
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}

namespace RemSolution.Application.Features.MarketplaceSearch.Queries.GetAgencyReviewsQuery
{
    public class GetAgencyReviewsQueryValidator : AbstractValidator<GetAgencyReviewsQuery>
    {
        public GetAgencyReviewsQueryValidator()
        {
            RuleFor(v => v.AgencyId).GreaterThan(0);
            RuleFor(v => v.PageSize).InclusiveBetween(1, 50);
        }
    }
}
