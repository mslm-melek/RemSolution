using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Documents;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using RemSolution.Application.Features.DocumentTemplate.Commands.CreateDocumentTemplateCommand;

namespace RemSolution.Application.Features.DocumentTemplate.Commands.UpdateDocumentTemplateCommand
{
    // Rewrites a template's layout and bindings.
    //
    // Editing a template does NOT touch documents already issued from it: those are
    // archived PDFs (see the Contract entity), which is what makes editing safe.
    [Authorize(Policy = Policies.AgencyOrPlatformAdmin)]
    [Auditable("UpdateDocumentTemplate", "DocumentTemplate")]
    public record UpdateDocumentTemplateCommand : IRequest, IDocumentTemplatePayload
    {
        public int Id { get; init; }

        /// <summary>The row version the client last read; a stale write is a 409 (see P.8).</summary>
        public byte[]? RowVersion { get; init; }

        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Kind is part of the payload for validation (it decides which data paths
        /// are legal) but is NOT reassigned: turning a contract template into an
        /// invoice one would silently invalidate its bindings. Changing kind means
        /// creating a new template.
        /// </summary>
        public DocumentTemplateKind Kind { get; init; }

        public string Language { get; init; } = Languages.Default;
        public List<DocumentBlock> Blocks { get; init; } = new();
        public List<DocumentTemplateFieldInput>? Fields { get; init; }
    }

    public class UpdateDocumentTemplateCommandHandler : IRequestHandler<UpdateDocumentTemplateCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;
        private readonly TimeProvider _dateTime;

        public UpdateDocumentTemplateCommandHandler(
            IApplicationDbContext context, ITenantProvider tenant, TimeProvider dateTime)
        {
            _context = context;
            _tenant = tenant;
            _dateTime = dateTime;
        }

        public async Task Handle(UpdateDocumentTemplateCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.DocumentTemplates
                .Include(t => t.Fields)
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            await Entitlements.EnsureFeatureAsync(
                _context, _tenant, _dateTime,
                CreateDocumentTemplateCommandHandler.FeatureFor(entity.Kind), cancellationToken);

            _context.SetOriginalRowVersion(entity, request.RowVersion);

            entity.Name = request.Name.Trim();
            entity.Language = request.Language;
            entity.BlocksJson = DocumentTemplateBlocks.Serialize(request.Blocks);

            // Bindings are replaced wholesale: the editor always submits the full
            // set, and reconciling row-by-row would be a lot of code to reach the
            // same state. Cascade-delete on the child collection handles the
            // removals.
            var replacement = DocumentTemplatePayloadMapper.ToFields(request with { Kind = entity.Kind });

            _context.DocumentTemplateFields.RemoveRange(
                entity.Fields ?? new List<Domain.Entities.DocumentTemplateField>());

            entity.Fields = replacement;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

namespace RemSolution.Application.Features.DocumentTemplate.Commands.UpdateDocumentTemplateCommand
{
    public class UpdateDocumentTemplateCommandValidator
        : DocumentTemplatePayloadValidator<UpdateDocumentTemplateCommand>
    {
        public UpdateDocumentTemplateCommandValidator(ILocalizer localizer) : base(localizer)
        {
            RuleFor(v => v.Id).GreaterThan(0);
        }
    }
}
