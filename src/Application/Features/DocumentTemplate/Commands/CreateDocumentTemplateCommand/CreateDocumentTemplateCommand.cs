using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Documents;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using TemplateEntity = RemSolution.Domain.Entities.DocumentTemplate;

namespace RemSolution.Application.Features.DocumentTemplate.Commands.CreateDocumentTemplateCommand
{
    // Saves a new contract or invoice layout for the agency — typed from scratch,
    // cloned from a shipped example, or imported from a Word/HTML/text file (the
    // import command produces the blocks; this one stores them).
    //
    // Managing paperwork layout is an administrator's job, not a per-permission
    // staff action, so this follows the reference-catalog pattern
    // (Policies.AgencyOrPlatformAdmin) rather than adding a permission. The FEATURE
    // gate has to be imperative: which of Contracts/Factures applies depends on the
    // template's Kind, which [RequiresFeature] cannot express.
    [Authorize(Policy = Policies.AgencyOrPlatformAdmin)]
    [Auditable("CreateDocumentTemplate", "DocumentTemplate")]
    public record CreateDocumentTemplateCommand : IRequest<int>, IDocumentTemplatePayload
    {
        public string Name { get; init; } = string.Empty;
        public DocumentTemplateKind Kind { get; init; }
        public string Language { get; init; } = Languages.Default;
        public List<DocumentBlock> Blocks { get; init; } = new();
        public List<DocumentTemplateFieldInput>? Fields { get; init; }
    }

    public class CreateDocumentTemplateCommandHandler : IRequestHandler<CreateDocumentTemplateCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;
        private readonly TimeProvider _dateTime;

        public CreateDocumentTemplateCommandHandler(
            IApplicationDbContext context, ITenantProvider tenant, TimeProvider dateTime)
        {
            _context = context;
            _tenant = tenant;
            _dateTime = dateTime;
        }

        public async Task<int> Handle(CreateDocumentTemplateCommand request, CancellationToken cancellationToken)
        {
            await Entitlements.EnsureFeatureAsync(
                _context, _tenant, _dateTime, FeatureFor(request.Kind), cancellationToken);

            var entity = new TemplateEntity
            {
                Name = request.Name.Trim(),
                Kind = request.Kind,
                Language = request.Language,
                IsActive = true,
                BlocksJson = DocumentTemplateBlocks.Serialize(request.Blocks),
                Fields = DocumentTemplatePayloadMapper.ToFields(request)
                // AgencyId is stamped by TenantEntityInterceptor on insert.
            };

            // Deciding whether this is the first of its kind and inserting it must
            // be atomic, or two concurrent creates could both see none and both
            // claim the default.
            await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
            await _context.AcquireTenantWriteLockAsync(cancellationToken);

            // The agency's first template for this kind and language becomes the
            // default: otherwise it would sit there unused while generation kept
            // falling back to the shipped example, which reads as a bug.
            entity.IsDefault = !await _context.DocumentTemplates
                .AnyAsync(t => t.Kind == request.Kind && t.Language == request.Language, cancellationToken);

            _context.DocumentTemplates.Add(entity);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return entity.Id;
        }

        internal static string FeatureFor(DocumentTemplateKind kind) =>
            kind == DocumentTemplateKind.Facture ? FeatureFlags.Factures : FeatureFlags.Contracts;
    }
}

namespace RemSolution.Application.Features.DocumentTemplate.Commands.CreateDocumentTemplateCommand
{
    public class CreateDocumentTemplateCommandValidator
        : DocumentTemplatePayloadValidator<CreateDocumentTemplateCommand>
    {
        public CreateDocumentTemplateCommandValidator(ILocalizer localizer) : base(localizer)
        {
        }
    }
}
