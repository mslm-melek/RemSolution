using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.DocumentTemplate.Commands.SetDocumentTemplateActiveCommand
{
    // Retires a template or brings it back. Deliberately not a delete: documents
    // already issued name the template that produced them, and that trail must
    // survive the layout being replaced.
    [Authorize(Policy = Policies.AgencyOrPlatformAdmin)]
    [Auditable("SetDocumentTemplateActive", "DocumentTemplate")]
    public record SetDocumentTemplateActiveCommand(int Id, bool IsActive) : IRequest;

    public class SetDocumentTemplateActiveCommandHandler : IRequestHandler<SetDocumentTemplateActiveCommand>
    {
        private readonly IApplicationDbContext _context;

        public SetDocumentTemplateActiveCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Handle(SetDocumentTemplateActiveCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.DocumentTemplates
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            entity.IsActive = request.IsActive;

            // Retiring the default gives up the default too, rather than leaving a
            // dangling one that generation would skip anyway. The agency falls back
            // to the shipped example until it names a new default.
            if (!request.IsActive)
            {
                entity.IsDefault = false;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
