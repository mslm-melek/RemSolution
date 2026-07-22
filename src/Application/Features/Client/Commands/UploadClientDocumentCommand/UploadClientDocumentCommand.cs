using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Client.Commands.UploadClientDocumentCommand
{
    // Stores the document image via IFileStorage and persists the returned
    // URL on the matching Client column. Returns that URL. Auditable because
    // it irreversibly deletes the previously stored document.
    // ISensitiveRequest: carries the raw document stream — must never be
    // destructured into logs.
    // Client.Update permission: replacing a client's identity documents is an
    // edit of the client record, not a permission of its own.
    [Authorize(Policy = Permissions.ClientUpdate)]
    [RequiresFeature(FeatureFlags.Clients)]
    [Auditable("UploadClientDocument", "Client")]
    public record UploadClientDocumentCommand : IRequest<string>, ISensitiveRequest
    {
        public int ClientId { get; init; }
        public ClientDocumentType DocumentType { get; init; }
        public string FileName { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
        public long Length { get; init; }
        public Stream Content { get; init; } = Stream.Null;
    }

    public class UploadClientDocumentCommandHandler : IRequestHandler<UploadClientDocumentCommand, string>
    {
        private readonly IApplicationDbContext _context;
        private readonly IStoredFileService _storedFiles;

        public UploadClientDocumentCommandHandler(IApplicationDbContext context, IStoredFileService storedFiles)
        {
            _context = context;
            _storedFiles = storedFiles;
        }

        public async Task<string> Handle(UploadClientDocumentCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Clients
                .FindAsync(new object[] { request.ClientId }, cancellationToken);

            Guard.Against.NotFound(request.ClientId, entity);

            var documentType = MapDocumentType(request.DocumentType);
            var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
            var relativePath =
                $"agencies/{entity.AgencyId}/clients/{entity.Id}/{request.DocumentType.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}{extension}";

            var file = await _storedFiles.CreateAsync(
                request.Content, request.FileName, request.ContentType, documentType, relativePath, cancellationToken);

            // Capture the id of the document being replaced (navigations are not
            // lazy-loaded, so read the FK, not the reference) then point the
            // client at the new file. EF fixes up the FK on save.
            int? previousFileId;
            switch (request.DocumentType)
            {
                case ClientDocumentType.CIN:
                    previousFileId = entity.CINFileId;
                    entity.CINFile = file;
                    break;
                case ClientDocumentType.DrivingLicence:
                    previousFileId = entity.DrivingLicenceFileId;
                    entity.DrivingLicenceFile = file;
                    break;
                case ClientDocumentType.Passeport:
                    previousFileId = entity.PasseportFileId;
                    entity.PasseportFile = file;
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled document type '{request.DocumentType}'.");
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // The row change did not commit (e.g. the write was blocked by
                // subscription enforcement): remove the just-written file so a
                // rejected upload leaves nothing publicly served. A deduped
                // upload reused an existing file, which the orphan check keeps.
                await _storedFiles.DeletePhysicalIfOrphanAsync(file.Path, file.Url, CancellationToken.None);
                throw;
            }

            // Only after the new document is durably attached: drop the replaced
            // record, then delete its bytes if nothing else references them (it
            // may share a physical file with the new upload, or another record).
            if (previousFileId is int prevId)
            {
                var previous = await _context.StoredFiles
                    .FirstOrDefaultAsync(f => f.Id == prevId, cancellationToken);

                if (previous is not null)
                {
                    _context.StoredFiles.Remove(previous);
                    await _context.SaveChangesAsync(cancellationToken);
                    await _storedFiles.DeletePhysicalIfOrphanAsync(previous.Path, previous.Url, cancellationToken);
                }
            }

            return file.Url;
        }

        private static Domain.Enums.DocumentType MapDocumentType(ClientDocumentType type) => type switch
        {
            ClientDocumentType.CIN => Domain.Enums.DocumentType.CIN,
            ClientDocumentType.DrivingLicence => Domain.Enums.DocumentType.DrivingLicence,
            ClientDocumentType.Passeport => Domain.Enums.DocumentType.Passeport,
            _ => throw new InvalidOperationException($"Unhandled document type '{type}'.")
        };
    }
}
