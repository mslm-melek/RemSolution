using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Branch.Commands.DeleteBranchCommand
{
    [Authorize(Roles = Roles.AgencyAdministrator)]
    [RequiresFeature(FeatureFlags.Branches)]
    [Auditable("DeleteBranch", "Branch")]
    public record DeleteBranchCommand(int Id) : IRequest;

    public class DeleteBranchCommandHandler : IRequestHandler<DeleteBranchCommand>
    {
        private readonly IApplicationDbContext _context;

        public DeleteBranchCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeleteBranchCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Branches
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            _context.Branches.Remove(entity);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
