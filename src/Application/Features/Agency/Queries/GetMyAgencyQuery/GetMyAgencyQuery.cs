using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Agency.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Agency.Queries.GetMyAgencyQuery
{
    // An agency administrator reads their OWN agency (tenant from the caller's
    // claim — never from the request). Agency is platform-level, not an
    // ITenantEntity, so there is no query filter behind this: the id predicate
    // below is the whole of the isolation.
    [Authorize(Roles = Roles.AgencyAdministrator)]
    public record GetMyAgencyQuery : IRequest<AgencyDto>;

    public class GetMyAgencyQueryHandler : IRequestHandler<GetMyAgencyQuery, AgencyDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;

        public GetMyAgencyQueryHandler(IApplicationDbContext context, ITenantProvider tenant)
        {
            _context = context;
            _tenant = tenant;
        }

        public async Task<AgencyDto> Handle(GetMyAgencyQuery request, CancellationToken cancellationToken)
        {
            if (_tenant.AgencyId is not int agencyId)
            {
                throw new ForbiddenAccessException();
            }

            var agency = await _context.Agencies
                .Where(a => a.Id == agencyId)
                .ProjectToType<AgencyDto>()
                .FirstOrDefaultAsync(cancellationToken);

            if (agency == null)
                throw new NotFoundException(nameof(Domain.Entities.Agency), agencyId.ToString());

            return agency;
        }
    }
}
