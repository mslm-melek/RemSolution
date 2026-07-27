using System.Globalization;
using RemSolution.Application.Common.Documents;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.DocumentTemplate.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.DocumentTemplate.Queries.GetDocumentTemplateExamplesQuery
{
    // The platform's read-only starting points, for the "start from an example"
    // picker. No database is touched: examples are code (see DocumentTemplateExamples).
    //
    // Deliberately takes NO language argument. An example's text comes from the
    // shared .resx, which resolves against the REQUEST's culture, so a caller-chosen
    // language could only disagree with the words actually returned — and cloning
    // that would leave the agency with a template tagged "ar" full of French
    // clauses, printing the wrong language to the customer. The example is always in
    // the language the caller is working in; to build one in another language, switch
    // language and clone again.
    [Authorize(Policy = Policies.AgencyOrPlatformAdmin)]
    public record GetDocumentTemplateExamplesQuery : IRequest<IList<DocumentTemplateExampleDto>>;

    public class GetDocumentTemplateExamplesQueryHandler
        : IRequestHandler<GetDocumentTemplateExamplesQuery, IList<DocumentTemplateExampleDto>>
    {
        private readonly DocumentTemplateExamples _examples;

        public GetDocumentTemplateExamplesQueryHandler(DocumentTemplateExamples examples)
        {
            _examples = examples;
        }

        public Task<IList<DocumentTemplateExampleDto>> Handle(
            GetDocumentTemplateExamplesQuery request, CancellationToken cancellationToken)
        {
            var language = Languages.Normalize(CultureInfo.CurrentUICulture.Name) ?? Languages.Default;

            IList<DocumentTemplateExampleDto> examples = _examples.All(language)
                .Select(example => new DocumentTemplateExampleDto
                {
                    Key = example.Key,
                    Name = example.Name,
                    Kind = example.Kind,
                    Language = example.Language,
                    Blocks = example.Blocks.ToList()
                })
                .ToList();

            return Task.FromResult(examples);
        }
    }
}
