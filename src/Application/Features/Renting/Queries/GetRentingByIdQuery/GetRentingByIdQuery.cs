using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Renting.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Renting.Queries.GetRentingByIdQuery
{
    [Authorize(Policy = Permissions.RentingRead)]
    [RequiresFeature(FeatureFlags.Rentings)]
    public record GetRentingByIdQuery(int Id) : IRequest<RentingDto?>;

    public class GetRentingByIdQueryHandler : IRequestHandler<GetRentingByIdQuery, RentingDto?>
    {
        private readonly IApplicationDbContext _context;

        public GetRentingByIdQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<RentingDto?> Handle(GetRentingByIdQuery request, CancellationToken cancellationToken)
        {
            var renting = await _context.Rentings
                .Where(r => r.Id == request.Id)
                .ProjectToType<RentingDto>()
                .FirstOrDefaultAsync(cancellationToken);

            if (renting == null)
                throw new NotFoundException("Renting", request.Id.ToString());

            return renting;
        }
    }
}
