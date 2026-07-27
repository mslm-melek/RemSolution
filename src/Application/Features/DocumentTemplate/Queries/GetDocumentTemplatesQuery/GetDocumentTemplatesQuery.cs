using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.DocumentTemplate.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.DocumentTemplate.Queries.GetDocumentTemplatesQuery
{
    // The agency's templates, for the admin screen and for the agent's picker on the
    // renting form.
    //
    // Read by staff, not just administrators: an agent choosing which contract to
    // print needs the list. Writes stay administrator-only. No [RequiresFeature]:
    // the query spans both kinds, and a filtered read of an off module returns
    // nothing anyway rather than leaking.
    [Authorize(Policy = Permissions.RentingRead)]
    public record GetDocumentTemplatesQuery : IRequest<IList<DocumentTemplateDto>>
    {
        /// <summary>Null returns both kinds.</summary>
        public DocumentTemplateKind? Kind { get; init; }

        /// <summary>Null returns every language.</summary>
        public string? Language { get; init; }

        /// <summary>
        /// False (the default) hides retired templates — what a picker wants. The
        /// admin screen passes true to manage them.
        /// </summary>
        public bool IncludeInactive { get; init; }
    }

    public class GetDocumentTemplatesQueryHandler
        : IRequestHandler<GetDocumentTemplatesQuery, IList<DocumentTemplateDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetDocumentTemplatesQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<DocumentTemplateDto>> Handle(
            GetDocumentTemplatesQuery request, CancellationToken cancellationToken)
        {
            var query = _context.DocumentTemplates
                .AsNoTracking()
                .Include(t => t.Fields)
                .AsQueryable();

            if (request.Kind is DocumentTemplateKind kind)
            {
                query = query.Where(t => t.Kind == kind);
            }

            if (!string.IsNullOrWhiteSpace(request.Language))
            {
                query = query.Where(t => t.Language == request.Language);
            }

            if (!request.IncludeInactive)
            {
                query = query.Where(t => t.IsActive);
            }

            // Defaults first, then alphabetically: the default is what the agent
            // wants nine times out of ten.
            var templates = await query
                .OrderByDescending(t => t.IsDefault)
                .ThenBy(t => t.Name)
                .ToListAsync(cancellationToken);

            // Projected in memory, not by the database: the DTO's Blocks are
            // deserialized from the stored JSON, which EF cannot translate.
            return templates.Adapt<List<DocumentTemplateDto>>();
        }
    }
}
