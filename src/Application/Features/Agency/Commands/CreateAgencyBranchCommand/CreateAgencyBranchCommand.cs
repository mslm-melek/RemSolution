using RemSolution.Application.Common.Geo;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Agency.Commands.CreateAgencyBranchCommand
{
    // The platform administrator adds a branch to an agency they are editing.
    // The agency's own administrator uses CreateBranchCommand instead, which is
    // feature-gated and takes its tenant from the caller's claim; here the agency
    // is named explicitly because the caller belongs to none.
    [Authorize(Roles = Roles.PlatformAdministrator)]
    public record CreateAgencyBranchCommand : IRequest<int>
    {
        public int AgencyId { get; init; }
        public string Name { get; init; } = string.Empty;
        public int CountryId { get; init; }
        public string? Address { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
    }

    public class CreateAgencyBranchCommandHandler : IRequestHandler<CreateAgencyBranchCommand, int>
    {
        private readonly IApplicationDbContext _context;

        public CreateAgencyBranchCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> Handle(CreateAgencyBranchCommand request, CancellationToken cancellationToken)
        {
            var agency = await _context.Agencies
                .FindAsync(new object[] { request.AgencyId }, cancellationToken);

            Guard.Against.NotFound(request.AgencyId, agency);

            var entity = new RemSolution.Domain.Entities.Branch
            {
                Name = request.Name,
                CountryId = request.CountryId,
                Address = request.Address,
                Location = GeoPoint.ToPoint(request.Latitude, request.Longitude),
            };

            // Acting as the agency lets the write interceptor stamp AgencyId, the
            // same way it does for the agency's own users. AgencyId is deliberately
            // not assigned above: the interceptor is the single place that decides
            // which tenant a row belongs to. Administrative, so a lapsed
            // subscription does not stop the app owner administering the agency.
            using var _ = AmbientTenant.PushAdministrative(request.AgencyId);

            _context.Branches.Add(entity);

            await _context.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
    }
}
