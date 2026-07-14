using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Branch.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Branch.Queries.GetBranchesQuery
{
    // Tenant query filter scopes the list to the current agency; no paging —
    // an agency has a handful of branches.
    [Authorize(Roles = Roles.AgencyAdministrator)]
    [RequiresFeature(FeatureFlags.Branches)]
    public record GetBranchesQuery : IRequest<IList<BranchDto>>;

    public class GetBranchesQueryHandler : IRequestHandler<GetBranchesQuery, IList<BranchDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetBranchesQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<BranchDto>> Handle(GetBranchesQuery request, CancellationToken cancellationToken)
        {
            return await _context.Branches
                .AsNoTracking()
                .OrderBy(b => b.Name)
                .ProjectToType<BranchDto>()
                .ToListAsync(cancellationToken);
        }
    }
}
