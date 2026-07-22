using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Car.Commands.UploadCarPhotoCommand
{
    // Stores the car photo via IStoredFileService and points the car at the
    // resulting StoredFile. Returns the public URL. Auditable because it
    // irreversibly replaces the previously stored photo. ISensitiveRequest:
    // carries the raw file stream — must never be destructured into logs.
    // Car.Update permission: setting a car's photo is an edit of the car record.
    [Authorize(Policy = Permissions.CarUpdate)]
    [RequiresFeature(FeatureFlags.Cars)]
    [Auditable("UploadCarPhoto", "Car")]
    public record UploadCarPhotoCommand : IRequest<string>, ISensitiveRequest
    {
        public int CarId { get; init; }
        public string FileName { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
        public long Length { get; init; }
        public Stream Content { get; init; } = Stream.Null;
    }

    public class UploadCarPhotoCommandHandler : IRequestHandler<UploadCarPhotoCommand, string>
    {
        private readonly IApplicationDbContext _context;
        private readonly IStoredFileService _storedFiles;

        public UploadCarPhotoCommandHandler(IApplicationDbContext context, IStoredFileService storedFiles)
        {
            _context = context;
            _storedFiles = storedFiles;
        }

        public async Task<string> Handle(UploadCarPhotoCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Cars
                .FindAsync(new object[] { request.CarId }, cancellationToken);

            Guard.Against.NotFound(request.CarId, entity);

            var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
            var relativePath =
                $"agencies/{entity.AgencyId}/cars/{entity.Id}/photo-{Guid.NewGuid():N}{extension}";

            var file = await _storedFiles.CreateAsync(
                request.Content, request.FileName, request.ContentType, DocumentType.CarPhoto, relativePath, cancellationToken);

            // Navigations are not lazy-loaded: read the FK, then repoint it.
            var previousFileId = entity.PhotoFileId;
            entity.PhotoFile = file;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                await _storedFiles.DeletePhysicalIfOrphanAsync(file.Path, file.Url, CancellationToken.None);
                throw;
            }

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
    }
}
