using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.DocumentTemplate.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.DocumentTemplate.Queries.GetDocumentPlaceholdersQuery
{
    // Every value a template of this kind can pull from a booking: the editor's
    // "insert a field" palette and the binding dropdown. Pure catalog — no database.
    [Authorize(Policy = Policies.AgencyOrPlatformAdmin)]
    public record GetDocumentPlaceholdersQuery(DocumentTemplateKind Kind)
        : IRequest<IList<DocumentPlaceholderDto>>;

    public class GetDocumentPlaceholdersQueryHandler
        : IRequestHandler<GetDocumentPlaceholdersQuery, IList<DocumentPlaceholderDto>>
    {
        public Task<IList<DocumentPlaceholderDto>> Handle(
            GetDocumentPlaceholdersQuery request, CancellationToken cancellationToken)
        {
            IList<DocumentPlaceholderDto> placeholders = DocumentPlaceholders.For(request.Kind)
                .Select(path => new DocumentPlaceholderDto
                {
                    Path = path,
                    Token = $"{{{{{path}}}}}",
                    // Leading segment: the SPA groups the palette by it, and a path
                    // without a dot groups under itself.
                    Group = path.Contains('.') ? path[..path.IndexOf('.')] : path
                })
                .ToList();

            return Task.FromResult(placeholders);
        }
    }
}
