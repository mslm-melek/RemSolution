using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Facture.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Facture.Queries.GetFacturesByRentingQuery
{
    [Authorize(Policy = Permissions.FactureRead)]
    [RequiresFeature(FeatureFlags.Factures)]
    public record GetFacturesByRentingQuery(int RentingId) : IRequest<IList<FactureDto>>;

    public class GetFacturesByRentingQueryHandler
        : IRequestHandler<GetFacturesByRentingQuery, IList<FactureDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetFacturesByRentingQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<FactureDto>> Handle(
            GetFacturesByRentingQuery request, CancellationToken cancellationToken)
        {
            return await _context.Factures
                .AsNoTracking()
                .Where(f => f.RentingId == request.RentingId)
                .OrderByDescending(f => f.SequenceNumber)
                .ProjectToType<FactureDto>()
                .ToListAsync(cancellationToken);
        }
    }
}
