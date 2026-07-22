using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Client.Commands.FlagClientCommand
{
    // Raising or clearing the per-agency bad-client flag is a moderation action
    // kept in its own slice, separate from routine client edits. It reuses the
    // Client.Update permission (no distinct grant) but records its own audit
    // action so the flag history is queryable apart from field edits.
    // Auditable: the interceptor captures the before/after IsFlagged + Notes
    // from the change tracker when the handler saves.
    [Authorize(Policy = Permissions.ClientUpdate)]
    [RequiresFeature(FeatureFlags.Clients)]
    [Auditable("FlagClient", "Client")]
    public record FlagClientCommand : IRequest
    {
        public int Id { get; init; }
        public bool IsFlagged { get; init; }
        // Reason for the flag; cleared alongside the flag when unflagging is the
        // caller's choice — the handler stores whatever is sent.
        public string? Notes { get; init; }
    }

    public class FlagClientCommandHandler : IRequestHandler<FlagClientCommand>
    {
        private readonly IApplicationDbContext _context;

        public FlagClientCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(FlagClientCommand request, CancellationToken cancellationToken)
        {
            // Global query filters scope the lookup to the current tenant, so a
            // client from another agency is simply not found — the flag can
            // never be raised cross-tenant.
            var entity = await _context.Clients
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.IsFlagged = request.IsFlagged;
            entity.Notes = request.Notes;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
