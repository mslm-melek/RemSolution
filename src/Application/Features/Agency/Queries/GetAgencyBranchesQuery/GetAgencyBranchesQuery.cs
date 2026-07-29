using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Application.Features.Branch.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Agency.Queries.GetAgencyBranchesQuery
{
    // The platform administrator reads one agency's branches while editing it.
    // Not feature-gated, unlike the agency's own GetBranchesQuery: whether the
    // agency's plan includes the Branches module governs what the agency's staff
    // can reach, not whether the app owner can set the agency up.
    [Authorize(Roles = Roles.PlatformAdministrator)]
    public record GetAgencyBranchesQuery(int AgencyId) : IRequest<IList<BranchDto>>;

    public class GetAgencyBranchesQueryHandler : IRequestHandler<GetAgencyBranchesQuery, IList<BranchDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetAgencyBranchesQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<BranchDto>> Handle(GetAgencyBranchesQuery request, CancellationToken cancellationToken)
        {
            var agency = await _context.Agencies
                .FindAsync(new object[] { request.AgencyId }, cancellationToken);

            Guard.Against.NotFound(request.AgencyId, agency);

            // Branch is an ITenantEntity and the caller has no tenant of their
            // own, so the query filter would match nothing. Acting as the agency
            // being edited is what scopes the read to it — no filter bypass, and
            // the same predicate its own users get.
            using var _ = AmbientTenant.Push(request.AgencyId);

            return await _context.Branches
                .AsNoTracking()
                .OrderBy(b => b.Name)
                .ProjectToType<BranchDto>()
                .ToListAsync(cancellationToken);
        }
    }
}
