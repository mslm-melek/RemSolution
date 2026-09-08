using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Application.Features.Agency.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Agency.Queries.GetAgencyPublicationQuery
{
    // Where an agency stands with the marketplace: live or not, and what it has
    // to put in the window. Read by the last step of the opening wizard and by
    // the agency's own page in the console.
    [Authorize(Roles = Roles.PlatformAdministrator)]
    public record GetAgencyPublicationQuery(int AgencyId) : IRequest<AgencyPublicationDto>;

    public class GetAgencyPublicationQueryHandler
        : IRequestHandler<GetAgencyPublicationQuery, AgencyPublicationDto>
    {
        private readonly IApplicationDbContext _context;

        public GetAgencyPublicationQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AgencyPublicationDto> Handle(
            GetAgencyPublicationQuery request, CancellationToken cancellationToken)
        {
            var agency = await _context.Agencies
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == request.AgencyId, cancellationToken);

            Guard.Against.NotFound(request.AgencyId, agency);

            // Car and Branch are tenant entities and the caller has no tenant of
            // their own, so the query filter would match nothing. Acting as the
            // agency scopes the read to it — no filter bypass, and the same
            // predicate its own users get (see GetAgencyBranchesQuery).
            using var _ = AmbientTenant.Push(request.AgencyId);

            var cars = _context.Cars.AsNoTracking();

            // The same three conditions MarketplaceCars.Offered applies to a car,
            // minus the agency gate this screen is about. Soft-delete comes from
            // the global filter here, which the public query has to re-apply by
            // hand because it reads without one.
            var offered = await cars
                .CountAsync(c => c.Status == CarStatus.Active && c.DailyRate != null, cancellationToken);

            var total = await cars.CountAsync(cancellationToken);
            var branches = await _context.Branches.AsNoTracking().CountAsync(cancellationToken);

            return new AgencyPublicationDto
            {
                AgencyId = agency.Id,
                PublishedAt = agency.PublishedAt,
                IsPublished = agency.IsPublished,
                OfferedCars = offered,
                TotalCars = total,
                Branches = branches,
                // Branches are not required: a single-site agency that collects
                // at its own address is a real business, and the address is on
                // the agency itself.
                CanPublish = offered > 0,
            };
        }
    }
}
