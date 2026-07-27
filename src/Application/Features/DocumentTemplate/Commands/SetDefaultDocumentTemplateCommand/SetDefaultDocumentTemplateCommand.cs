using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.DocumentTemplate.Commands.SetDefaultDocumentTemplateCommand
{
    // Makes one template the agency's default for its kind and language — the one
    // generation picks when the agent does not choose.
    [Authorize(Policy = Policies.AgencyOrPlatformAdmin)]
    [Auditable("SetDefaultDocumentTemplate", "DocumentTemplate")]
    public record SetDefaultDocumentTemplateCommand(int Id) : IRequest;

    public class SetDefaultDocumentTemplateCommandHandler : IRequestHandler<SetDefaultDocumentTemplateCommand>
    {
        private readonly IApplicationDbContext _context;

        public SetDefaultDocumentTemplateCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(SetDefaultDocumentTemplateCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.DocumentTemplates
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            // Clearing the old default and setting the new one is one logical
            // change; under the write lock so a concurrent swap cannot leave the
            // agency with two defaults (nothing in the schema forbids it — see the
            // DocumentTemplate entity for why).
            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _context.AcquireTenantWriteLockAsync(cancellationToken);

            var siblings = await _context.DocumentTemplates
                .Where(t => t.Kind == entity.Kind && t.Language == entity.Language && t.IsDefault)
                .ToListAsync(cancellationToken);

            foreach (var sibling in siblings)
            {
                sibling.IsDefault = false;
            }

            entity.IsDefault = true;

            // A retired template cannot be the default: generation skips inactive
            // ones, so it would silently fall through to the shipped example.
            entity.IsActive = true;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }
}
