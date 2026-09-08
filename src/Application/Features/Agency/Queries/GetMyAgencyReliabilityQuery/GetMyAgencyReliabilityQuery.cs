using RemSolution.Application.Common.Agencies;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Agency.Queries.GetMyAgencyReliabilityQuery
{
    // The figure the marketplace shows about this agency, shown back to the
    // agency itself: nobody should learn their public score from a customer.
    // Counted from the agency's own (tenant-filtered) rows, through the same
    // predicates the shopfront uses.
    [Authorize(Roles = Roles.AgencyAdministrator)]
    public record GetMyAgencyReliabilityQuery : IRequest<AgencyReliabilityDto>;

    public class GetMyAgencyReliabilityQueryHandler
        : IRequestHandler<GetMyAgencyReliabilityQuery, AgencyReliabilityDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;

        public GetMyAgencyReliabilityQueryHandler(
            IApplicationDbContext context, ITenantProvider tenant)
        {
            _context = context;
            _tenant = tenant;
        }

        public async Task<AgencyReliabilityDto> Handle(
            GetMyAgencyReliabilityQuery request, CancellationToken cancellationToken)
        {
            if (_tenant.AgencyId is not int agencyId)
            {
                throw new ForbiddenAccessException();
            }

            var reliability = await AgencyReliabilityCounts.ComputeAsync(
                _context.Reservations.AsNoTracking(),
                // Reports are platform-level, so the AgencyId predicate inside
                // ComputeAsync is the whole of the isolation here.
                _context.AgencyReports.AsNoTracking(),
                agencyId,
                cancellationToken);

            return AgencyReliabilityDto.From(reliability);
        }
    }
}
