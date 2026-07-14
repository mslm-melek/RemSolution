using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Branch.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Branch.Queries.GetBranchByIdQuery
{
    [Authorize(Roles = Roles.AgencyAdministrator)]
    [RequiresFeature(FeatureFlags.Branches)]
    public record GetBranchByIdQuery(int Id) : IRequest<BranchDto>;

    public class GetBranchByIdQueryHandler : IRequestHandler<GetBranchByIdQuery, BranchDto>
    {
        private readonly IApplicationDbContext _context;

        public GetBranchByIdQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<BranchDto> Handle(GetBranchByIdQuery request, CancellationToken cancellationToken)
        {
            var branch = await _context.Branches
                .Where(b => b.Id == request.Id)
                .ProjectToType<BranchDto>()
                .FirstOrDefaultAsync(cancellationToken);

            if (branch == null)
                throw new NotFoundException(nameof(Branch), request.Id.ToString());

            return branch;
        }
    }
}
