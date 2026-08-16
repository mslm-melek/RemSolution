using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Renting.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Renting.Queries.GetRentingFeesQuery
{
    // The extra charges booked on one hire, oldest first — the order they were
    // established at the counter, which is the order the invoice prints them in.
    [Authorize(Policy = Permissions.RentingRead)]
    [RequiresFeature(FeatureFlags.Rentings)]
    public record GetRentingFeesQuery(int RentingId) : IRequest<IList<RentingFeeDto>>;

    public class GetRentingFeesQueryHandler : IRequestHandler<GetRentingFeesQuery, IList<RentingFeeDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetRentingFeesQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<RentingFeeDto>> Handle(
            GetRentingFeesQuery request, CancellationToken cancellationToken)
        {
            return await _context.RentingFees
                .AsNoTracking()
                .Where(f => f.RentingId == request.RentingId)
                .OrderBy(f => f.Id)
                .ProjectToType<RentingFeeDto>()
                .ToListAsync(cancellationToken);
        }
    }
}
