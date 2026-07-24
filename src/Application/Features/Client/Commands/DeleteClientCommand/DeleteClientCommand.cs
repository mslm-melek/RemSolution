using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Client.Commands.DeleteClientCommand
{
    [Authorize(Policy = Permissions.ClientDelete)]
    [RequiresFeature(FeatureFlags.Clients)]
    [Auditable("DeleteClient", "Client")]
    public record DeleteClientCommand(int Id) : IRequest;

    public class DeleteClientCommandHandler : IRequestHandler<DeleteClientCommand>
    {
        private readonly IApplicationDbContext _context;

        public DeleteClientCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(DeleteClientCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Clients
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            // Archive, don't erase: Client is ISoftDeletable, so
            // SoftDeleteInterceptor turns this Remove into an IsDeleted flag
            // update. The client, its identity documents and its rentings /
            // reservations / payments are all preserved (history and financial
            // records); the client is hidden by the global query filter.
            _context.Clients.Remove(entity);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
