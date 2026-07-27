using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Contract.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Contract.Queries.GetContractsByRentingQuery
{
    [Authorize(Policy = Permissions.ContractRead)]
    [RequiresFeature(FeatureFlags.Contracts)]
    public record GetContractsByRentingQuery(int RentingId) : IRequest<IList<ContractDto>>;

    public class GetContractsByRentingQueryHandler
        : IRequestHandler<GetContractsByRentingQuery, IList<ContractDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetContractsByRentingQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<ContractDto>> Handle(
            GetContractsByRentingQuery request, CancellationToken cancellationToken)
        {
            // Newest first: after a regeneration the current agreement is the one
            // the agent wants to hand over.
            return await _context.Contracts
                .AsNoTracking()
                .Where(c => c.RentingId == request.RentingId)
                .OrderByDescending(c => c.SequenceNumber)
                .ProjectToType<ContractDto>()
                .ToListAsync(cancellationToken);
        }
    }
}
