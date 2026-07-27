using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.DocumentTemplate.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.DocumentTemplate.Queries.GetDocumentTemplateByIdQuery
{
    // One template with its blocks and bindings, for the editor.
    [Authorize(Policy = Policies.AgencyOrPlatformAdmin)]
    public record GetDocumentTemplateByIdQuery(int Id) : IRequest<DocumentTemplateDto?>;

    public class GetDocumentTemplateByIdQueryHandler
        : IRequestHandler<GetDocumentTemplateByIdQuery, DocumentTemplateDto?>
    {
        private readonly IApplicationDbContext _context;

        public GetDocumentTemplateByIdQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DocumentTemplateDto?> Handle(
            GetDocumentTemplateByIdQuery request, CancellationToken cancellationToken)
        {
            // Tenant-scoped by the global query filter, so another agency's id is
            // simply not found.
            var template = await _context.DocumentTemplates
                .AsNoTracking()
                .Include(t => t.Fields)
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

            if (template is null)
            {
                throw new NotFoundException(nameof(DocumentTemplate), request.Id.ToString());
            }

            // Adapted in memory: the DTO's Blocks come from the stored JSON.
            return template.Adapt<DocumentTemplateDto>();
        }
    }
}
