using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.ExtraService.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.ExtraService.Queries.GetExtraServicesByRentingQuery
{
    [Authorize(Policy = Permissions.ExtraServiceRead)]
    [RequiresFeature(FeatureFlags.ExtraServices)]
    public record GetExtraServicesByRentingQuery(int RentingId) : IRequest<IList<ExtraServiceDto>>;

    public class GetExtraServicesByRentingQueryHandler
        : IRequestHandler<GetExtraServicesByRentingQuery, IList<ExtraServiceDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetExtraServicesByRentingQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<ExtraServiceDto>> Handle(
            GetExtraServicesByRentingQuery request, CancellationToken cancellationToken)
        {
            return await _context.ExtraServices
                .AsNoTracking()
                .Where(e => e.RentingId == request.RentingId)
                .ProjectToType<ExtraServiceDto>()
                .ToListAsync(cancellationToken);
        }
    }
}
