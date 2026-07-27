using RemSolution.Application.Common.Documents;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.DocumentTemplate.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.DocumentTemplate.Commands.ImportDocumentTemplateCommand
{
    /// <summary>
    /// An imported file turned into an unsaved template, ready to open in the
    /// editor.
    /// </summary>
    public class DocumentTemplateDraftDto
    {
        /// <summary>Suggested name, taken from the uploaded file.</summary>
        public string Name { get; init; } = string.Empty;

        public DocumentTemplateKind Kind { get; init; }
        public string Language { get; init; } = string.Empty;
        public IList<DocumentBlock> Blocks { get; init; } = new List<DocumentBlock>();

        /// <summary>
        /// Auto-bindings for whatever placeholders the file already contained.
        /// Usually empty — see <see cref="DocumentTemplateImport"/>.
        /// </summary>
        public IList<DocumentTemplateFieldDto> Fields { get; init; } = new List<DocumentTemplateFieldDto>();
    }

    // Reads an uploaded contract or invoice into blocks. Deliberately does NOT save
    // anything: the admin lands in the editor with the text already in, names it,
    // drops placeholders where the blanks were, and then saves through
    // CreateDocumentTemplateCommand. Importing straight to a row would leave
    // half-finished templates lying in the picker.
    //
    // ISensitiveRequest: carries the raw uploaded stream.
    [Authorize(Policy = Policies.AgencyOrPlatformAdmin)]
    public record ImportDocumentTemplateCommand : IRequest<DocumentTemplateDraftDto>, ISensitiveRequest
    {
        public DocumentTemplateKind Kind { get; init; }
        public string Language { get; init; } = Languages.Default;
        public string FileName { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
        public long Length { get; init; }
        public Stream Content { get; init; } = Stream.Null;
    }

    public class ImportDocumentTemplateCommandHandler
        : IRequestHandler<ImportDocumentTemplateCommand, DocumentTemplateDraftDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly IDocumentTemplateImporter _importer;
        private readonly ITenantProvider _tenant;
        private readonly TimeProvider _dateTime;
        private readonly IMapper _mapper;

        public ImportDocumentTemplateCommandHandler(
            IApplicationDbContext context,
            IDocumentTemplateImporter importer,
            ITenantProvider tenant,
            TimeProvider dateTime,
            IMapper mapper)
        {
            _context = context;
            _importer = importer;
            _tenant = tenant;
            _dateTime = dateTime;
            _mapper = mapper;
        }

        public async Task<DocumentTemplateDraftDto> Handle(
            ImportDocumentTemplateCommand request, CancellationToken cancellationToken)
        {
            await Entitlements.EnsureFeatureAsync(
                _context, _tenant, _dateTime,
                request.Kind == DocumentTemplateKind.Facture ? FeatureFlags.Factures : FeatureFlags.Contracts,
                cancellationToken);

            var import = await _importer.ImportAsync(
                request.Content, request.FileName, request.ContentType, cancellationToken);

            var fields = import.Placeholders
                .Select(placeholder => DocumentTemplateFields.AutoBind(placeholder, request.Kind))
                .ToList();

            return new DocumentTemplateDraftDto
            {
                // The file name is the closest thing to the admin's own name for it.
                Name = Path.GetFileNameWithoutExtension(request.FileName),
                Kind = request.Kind,
                Language = request.Language,
                Blocks = import.Blocks.ToList(),
                Fields = fields.Select(f => _mapper.Map<DocumentTemplateFieldDto>(f)).ToList()
            };
        }
    }
}

namespace RemSolution.Application.Features.DocumentTemplate.Commands.ImportDocumentTemplateCommand
{
    public class ImportDocumentTemplateCommandValidator : AbstractValidator<ImportDocumentTemplateCommand>
    {
        // A rental contract is a few pages of text; anything far larger is either
        // the wrong file or an image-heavy export whose pictures we drop anyway.
        private const long MaxLength = 5 * 1024 * 1024;

        public ImportDocumentTemplateCommandValidator(ILocalizer localizer)
        {
            RuleFor(v => v.Kind).IsInEnum();

            RuleFor(v => v.Language)
                .Must(Languages.IsSupported)
                    .WithMessage(_ => localizer["Validation.DocumentTemplate.LanguageUnsupported"]);

            RuleFor(v => v.FileName).NotEmpty().MaximumLength(260);

            RuleFor(v => v.Length)
                .GreaterThan(0)
                .LessThanOrEqualTo(MaxLength);
        }
    }
}
