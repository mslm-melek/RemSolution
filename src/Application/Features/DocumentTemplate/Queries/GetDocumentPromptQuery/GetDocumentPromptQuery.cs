using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.DocumentTemplate.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.DocumentTemplate.Queries.GetDocumentPromptQuery
{
    // What the agent must be asked before this document can be generated: the
    // ask-each-time placeholders of the template that would actually be used.
    //
    // Empty for both shipped examples, so an agency that has not built a template
    // never sees a prompt. Called by the renting form before it offers "generate".
    [Authorize(Policy = Permissions.RentingRead)]
    public record GetDocumentPromptQuery(DocumentTemplateKind Kind, int? TemplateId = null)
        : IRequest<IList<DocumentTemplateFieldDto>>;

    public class GetDocumentPromptQueryHandler
        : IRequestHandler<GetDocumentPromptQuery, IList<DocumentTemplateFieldDto>>
    {
        private readonly IRentalDocumentService _documents;
        private readonly IMapper _mapper;

        public GetDocumentPromptQueryHandler(IRentalDocumentService documents, IMapper mapper)
        {
            _documents = documents;
            _mapper = mapper;
        }

        public async Task<IList<DocumentTemplateFieldDto>> Handle(
            GetDocumentPromptQuery request, CancellationToken cancellationToken)
        {
            // Resolving which template applies is the generation path's own rule
            // (explicit → agency default → shipped example), so it is asked rather
            // than duplicated here — a prompt for a different template than the one
            // that ends up rendering would be worse than no prompt.
            var fields = await _documents.GetPromptFieldsAsync(
                request.Kind, request.TemplateId, cancellationToken);

            return fields.Select(f => _mapper.Map<DocumentTemplateFieldDto>(f)).ToList();
        }
    }
}
