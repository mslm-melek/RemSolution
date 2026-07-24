using Hangfire;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace RemSolution.Infrastructure.Imaging;

// The unit of work Hangfire runs for each uploaded car image: generate a
// thumbnail and a medium derivative out of band. Hangfire resolves this type
// from DI in its own scope per execution, so the injected DbContext /
// StoredFileService are scoped exactly as in a request. The job has no HTTP
// context, so it pushes its agency onto AmbientTenant — query filters and the
// tenant write-stamp then behave as they do in a request. A failure marks the
// image Failed and leaves the original usable; Hangfire also retries.
public sealed class CarImageProcessingJob
{
    private const int ThumbnailMaxPx = 200;
    private const int MediumMaxPx = 800;

    private readonly IApplicationDbContext _context;
    private readonly IFileStorage _storage;
    private readonly IStoredFileService _storedFiles;
    private readonly IImageProcessor _processor;
    private readonly ILogger<CarImageProcessingJob> _logger;

    public CarImageProcessingJob(
        IApplicationDbContext context,
        IFileStorage storage,
        IStoredFileService storedFiles,
        IImageProcessor processor,
        ILogger<CarImageProcessingJob> logger)
    {
        _context = context;
        _storage = storage;
        _storedFiles = storedFiles;
        _processor = processor;
        _logger = logger;
    }

    // Capped retries: a genuinely undecodable image should fail a few times and
    // stop, not churn the default 10 attempts.
    [AutomaticRetry(Attempts = 3)]
    public async Task ProcessAsync(int carImageId, int agencyId)
    {
        // Act as the job's agency for every tenant-scoped read/write below.
        using var _ = AmbientTenant.Push(agencyId);

        var image = await _context.CarImages
            .Include(i => i.OriginalFile)
            .Include(i => i.ThumbnailFile)
            .Include(i => i.MediumFile)
            .FirstOrDefaultAsync(i => i.Id == carImageId);

        if (image?.OriginalFile is null)
        {
            // Row deleted before we got to it, or no original — nothing to do.
            return;
        }

        // Derivatives from a previous (completed or partial) attempt are being
        // replaced; collect them so their rows/bytes are cleaned up on success
        // rather than orphaned when a re-run reassigns the FKs.
        var stale = new[] { image.ThumbnailFile, image.MediumFile }
            .Where(f => f is not null).Select(f => f!).DistinctBy(f => f.Id).ToList();

        image.ProcessingStatus = ImageProcessingStatus.Processing;
        await _context.SaveChangesAsync(CancellationToken.None);

        StoredFile? newThumbnail = null;
        StoredFile? newMedium = null;
        try
        {
            var originalBytes = await ReadAllBytesAsync(_storage, image.OriginalFile.Url);

            // Both resizes run before anything is persisted, so an undecodable
            // image fails here having created nothing.
            var thumbnailBytes = _processor.ResizeToJpeg(originalBytes, ThumbnailMaxPx);
            var mediumBytes = _processor.ResizeToJpeg(originalBytes, MediumMaxPx);

            var basePath = $"agencies/{image.AgencyId}/cars/{image.CarId}/images/{image.Id}";

            newThumbnail = await CreateDerivativeAsync(
                _storedFiles, thumbnailBytes, $"{basePath}/thumb-{Guid.NewGuid():N}.jpg");
            newMedium = await CreateDerivativeAsync(
                _storedFiles, mediumBytes, $"{basePath}/medium-{Guid.NewGuid():N}.jpg");

            image.ThumbnailFile = newThumbnail;
            image.MediumFile = newMedium;
            image.ProcessingStatus = ImageProcessingStatus.Completed;

            // Drop the superseded derivative rows in the same commit (dependents
            // before principals; the FKs now point at the new files).
            _context.StoredFiles.RemoveRange(stale);

            await _context.SaveChangesAsync(CancellationToken.None);

            // Delete superseded bytes after commit; a deduped file shared by
            // another row (e.g. identical re-resize) is kept.
            foreach (var file in stale)
            {
                await _storedFiles.DeletePhysicalIfOrphanAsync(file.Path, file.Url);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to generate derivatives for car image {CarImageId}; leaving the original usable",
                image.Id);

            image.ProcessingStatus = ImageProcessingStatus.Failed;

            // Discard any derivative this attempt created but did not commit, so
            // the status save below can't persist (and orphan) it on retry.
            var discarded = new[] { newThumbnail, newMedium }
                .Where(f => f is not null && f != image.ThumbnailFile && f != image.MediumFile)
                .Select(f => f!).ToList();
            _context.StoredFiles.RemoveRange(discarded);

            await _context.SaveChangesAsync(CancellationToken.None);

            foreach (var file in discarded)
            {
                await _storedFiles.DeletePhysicalIfOrphanAsync(file.Path, file.Url);
            }

            throw; // Surface to Hangfire so the failure is recorded (and retried).
        }
    }

    private static async Task<byte[]> ReadAllBytesAsync(IFileStorage storage, string url)
    {
        await using var source = await storage.OpenReadAsync(url);
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private static Task<StoredFile> CreateDerivativeAsync(
        IStoredFileService storedFiles, byte[] bytes, string relativePath)
    {
        var stream = new MemoryStream(bytes, writable: false);
        return storedFiles.CreateAsync(
            stream, Path.GetFileName(relativePath), "image/jpeg",
            DocumentType.CarPhoto, relativePath);
    }
}
