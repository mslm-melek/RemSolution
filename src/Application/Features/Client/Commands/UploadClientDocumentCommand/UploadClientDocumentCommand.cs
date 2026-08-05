using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Imaging;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
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
        private readonly ClientPortraitFactory _portraits;

        public UploadClientDocumentCommandHandler(
            IApplicationDbContext context,
            IStoredFileService storedFiles,
            ClientPortraitFactory portraits)
        {
            _context = context;
            _storedFiles = storedFiles;
            _portraits = portraits;
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

            // A CIN is read twice — stored as the document, and cropped for the
            // client's portrait — so its bytes are buffered here rather than
            // handed straight to a forward-only consumer. Uploads are capped at a
            // few MB by the validator, as StoredFileService's own buffer relies on.
            var isCin = request.DocumentType == ClientDocumentType.CIN;
            byte[]? bytes = null;
            Stream content = request.Content;

            if (isCin)
            {
                using var buffer = new MemoryStream();
                await request.Content.CopyToAsync(buffer, cancellationToken);
                bytes = buffer.ToArray();
                content = new MemoryStream(bytes, writable: false);
            }

            var file = await _storedFiles.CreateAsync(
                content, request.FileName, request.ContentType, documentType, relativePath, cancellationToken);

            // Capture the ids of the records being replaced (navigations are not
            // lazy-loaded, so read the FKs, not the references) then point the
            // client at the new files. EF fixes up the FKs on save.
            var superseded = new List<int>(2);

            switch (request.DocumentType)
            {
                case ClientDocumentType.CIN:
                    if (entity.CINFileId is int cinId) superseded.Add(cinId);
                    entity.CINFile = file;
                    break;
                case ClientDocumentType.DrivingLicence:
                    if (entity.DrivingLicenceFileId is int licenceId) superseded.Add(licenceId);
                    entity.DrivingLicenceFile = file;
                    break;
                case ClientDocumentType.Passeport:
                    if (entity.PasseportFileId is int passportId) superseded.Add(passportId);
                    entity.PasseportFile = file;
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled document type '{request.DocumentType}'.");
            }

            // The portrait belongs to the CIN it was cut from, so a new CIN always
            // replaces it — including with nothing, when no face can be found on
            // the new image. Leaving the old face beside the new card would be
            // worse than showing initials.
            StoredFile? portrait = null;

            if (isCin)
            {
                if (entity.CINPortraitFileId is int previousPortraitId)
                {
                    superseded.Add(previousPortraitId);
                }

                portrait = await _portraits.TryCreateAsync(
                    entity.AgencyId, entity.Id, bytes!, cancellationToken);

                if (portrait is null)
                {
                    // Clear the FK as well as the navigation: EF cannot tell a
                    // reference nulled out from one that was simply never loaded,
                    // so the FK is what actually detaches the old portrait.
                    entity.CINPortraitFile = null;
                    entity.CINPortraitFileId = null;
                }
                else
                {
                    entity.CINPortraitFile = portrait;
                }
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // The row change did not commit (e.g. the write was blocked by
                // subscription enforcement): remove the just-written files so a
                // rejected upload leaves nothing publicly served. A deduped
                // upload reused an existing file, which the orphan check keeps.
                await _storedFiles.DeletePhysicalIfOrphanAsync(file.Path, file.Url, CancellationToken.None);

                if (portrait is not null)
                {
                    await _storedFiles.DeletePhysicalIfOrphanAsync(
                        portrait.Path, portrait.Url, CancellationToken.None);
                }

                throw;
            }

            // Only after the new document is durably attached: drop the replaced
            // records, then delete their bytes if nothing else references them
            // (they may share a physical file with the new upload, or another
            // record).
            await RemoveSupersededAsync(superseded, cancellationToken);

            return file.Url;
        }

        private async Task RemoveSupersededAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken)
        {
            if (ids.Count == 0)
            {
                return;
            }

            var previous = await _context.StoredFiles
                .Where(f => ids.Contains(f.Id))
                .ToListAsync(cancellationToken);

            if (previous.Count == 0)
            {
                return;
            }

            _context.StoredFiles.RemoveRange(previous);
            await _context.SaveChangesAsync(cancellationToken);

            foreach (var file in previous)
            {
                await _storedFiles.DeletePhysicalIfOrphanAsync(file.Path, file.Url, cancellationToken);
            }
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
