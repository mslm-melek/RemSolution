using RemSolution.Application.Common.Geo;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Agency.Commands.UpdateAgencyBranchCommand
{
    // The platform-administrator counterpart of UpdateBranchCommand — see
    // CreateAgencyBranchCommand for why the agency is named explicitly.
    [Authorize(Roles = Roles.PlatformAdministrator)]
    public record UpdateAgencyBranchCommand : IRequest
    {
        public int AgencyId { get; init; }
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int CountryId { get; init; }
        public string? Address { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
    }

    public class UpdateAgencyBranchCommandHandler : IRequestHandler<UpdateAgencyBranchCommand>
    {
        private readonly IApplicationDbContext _context;

        public UpdateAgencyBranchCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(UpdateAgencyBranchCommand request, CancellationToken cancellationToken)
        {
            // Acting as the agency for the read as well as the write: the tenant
            // filter is what makes a branch id belonging to some other agency come
            // back as a 404 here rather than being edited across tenants.
            // Administrative, so a lapsed subscription does not stop the app owner
            // administering the agency.
            using var _ = AmbientTenant.PushAdministrative(request.AgencyId);

            var entity = await _context.Branches
                .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.Name = request.Name;
            entity.CountryId = request.CountryId;
            entity.Address = request.Address;
            entity.Location = GeoPoint.ToPoint(request.Latitude, request.Longitude);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
