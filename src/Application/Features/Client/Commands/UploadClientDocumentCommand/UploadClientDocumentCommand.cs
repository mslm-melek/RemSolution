using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Client.Commands.UploadClientDocumentCommand
{
    // Stores the document image via IFileStorage and persists the returned
    // URL on the matching Client column. Returns that URL. Auditable because
    // it irreversibly deletes the previously stored document.
    // ISensitiveRequest: carries the raw document stream — must never be
    // destructured into logs.
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
        private readonly IFileStorage _fileStorage;

        public UploadClientDocumentCommandHandler(IApplicationDbContext context, IFileStorage fileStorage)
        {
            _context = context;
            _fileStorage = fileStorage;
        }

        public async Task<string> Handle(UploadClientDocumentCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Clients
                .FindAsync(new object[] { request.ClientId }, cancellationToken);

            Guard.Against.NotFound(request.ClientId, entity);

            var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
            var relativePath =
                $"agencies/{entity.AgencyId}/clients/{entity.Id}/{request.DocumentType.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}{extension}";

            var url = await _fileStorage.SaveAsync(request.Content, relativePath, request.ContentType, cancellationToken);

            string? previousUrl;
            switch (request.DocumentType)
            {
                case ClientDocumentType.CIN:
                    previousUrl = entity.CINImageUrl;
                    entity.CINImageUrl = url;
                    break;
                case ClientDocumentType.DrivingLicence:
                    previousUrl = entity.DrivingLicenceImageUrl;
                    entity.DrivingLicenceImageUrl = url;
                    break;
                case ClientDocumentType.Passeport:
                    previousUrl = entity.PasserportImageUrl;
                    entity.PasserportImageUrl = url;
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
                // rejected upload leaves nothing publicly served.
                await _fileStorage.DeleteAsync(url, CancellationToken.None);
                throw;
            }

            // Only after the row change is durable: a failed delete leaves an
            // orphan file, never a broken URL.
            if (!string.IsNullOrEmpty(previousUrl) && previousUrl != url)
                await _fileStorage.DeleteAsync(previousUrl, cancellationToken);

            return url;
        }
    }
}
