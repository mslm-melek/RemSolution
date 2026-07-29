using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Agency.Commands.DeleteAgencyBranchCommand
{
    // The platform-administrator counterpart of DeleteBranchCommand — see
    // CreateAgencyBranchCommand for why the agency is named explicitly.
    // Cars keep their rows: Car.BranchId is SetNull, so removing a branch
    // declassifies the cars parked there rather than blocking on them.
    [Authorize(Roles = Roles.PlatformAdministrator)]
    [Auditable("DeleteAgencyBranch", "Branch")]
    public record DeleteAgencyBranchCommand(int AgencyId, int Id) : IRequest;

    public class DeleteAgencyBranchCommandHandler : IRequestHandler<DeleteAgencyBranchCommand>
    {
        private readonly IApplicationDbContext _context;

        public DeleteAgencyBranchCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeleteAgencyBranchCommand request, CancellationToken cancellationToken)
        {
            // Acting as the agency for the read as well as the delete: the tenant
            // filter is what makes a branch id belonging to some other agency come
            // back as a 404 here rather than being deleted across tenants.
            // Administrative, so a lapsed subscription does not stop the app owner
            // administering the agency.
            using var _ = AmbientTenant.PushAdministrative(request.AgencyId);

            var entity = await _context.Branches
                .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            _context.Branches.Remove(entity);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
