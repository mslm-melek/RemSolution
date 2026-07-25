using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Renting.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Renting.Queries.GetRentingHistoryQuery
{
    [Authorize(Policy = Permissions.RentingRead)]
    [RequiresFeature(FeatureFlags.Rentings)]
    public record GetRentingHistoryQuery(int RentingId) : IRequest<IList<RentingHistoryDto>>;

    public class GetRentingHistoryQueryHandler
        : IRequestHandler<GetRentingHistoryQuery, IList<RentingHistoryDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetRentingHistoryQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<RentingHistoryDto>> Handle(
            GetRentingHistoryQuery request, CancellationToken cancellationToken)
        {
            return await _context.RentingHistories
                .AsNoTracking()
                .Where(h => h.RentingId == request.RentingId)
                .OrderByDescending(h => h.EndDate)
                .ProjectToType<RentingHistoryDto>()
                .ToListAsync(cancellationToken);
        }
    }
}
