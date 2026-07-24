using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Car.Commands.DeleteCarImageCommand
{
    // Removes one image from a car's gallery: deletes the CarImage row and its
    // original/thumbnail/medium StoredFile rows, then cleans the physical bytes
    // (kept while any deduped row still references them, as in DeleteCarCommand).
    // If the removed image was primary, the remaining image with the lowest
    // SortOrder is promoted, so a non-empty gallery always has exactly one
    // primary. Editing the gallery is an edit of the car: Car.Update.
    [Authorize(Policy = Permissions.CarUpdate)]
    [RequiresFeature(FeatureFlags.Cars)]
    [Auditable("DeleteCarImage", "Car")]
    public record DeleteCarImageCommand(int CarId, int ImageId) : IRequest;

    public class DeleteCarImageCommandHandler : IRequestHandler<DeleteCarImageCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IStoredFileService _storedFiles;

        public DeleteCarImageCommandHandler(IApplicationDbContext context, IStoredFileService storedFiles)
        {
            _context = context;
            _storedFiles = storedFiles;
        }

        public async Task Handle(DeleteCarImageCommand request, CancellationToken cancellationToken)
        {
            // Load the whole gallery (tenant-filtered) so we can both delete the
            // target and pick its replacement primary without a second query.
            var images = await _context.CarImages
                .Include(i => i.OriginalFile)
                .Include(i => i.ThumbnailFile)
                .Include(i => i.MediumFile)
                .Where(i => i.CarId == request.CarId)
                .ToListAsync(cancellationToken);

            var image = images.FirstOrDefault(i => i.Id == request.ImageId);
            Guard.Against.NotFound(request.ImageId, image);

            var files = new[] { image.OriginalFile, image.ThumbnailFile, image.MediumFile }
                .Where(f => f is not null)
                .Select(f => f!)
                .DistinctBy(f => f.Id)
                .ToList();

            _context.CarImages.Remove(image);
            _context.StoredFiles.RemoveRange(files);

            // Promote a new primary if we removed the primary one.
            if (image.IsPrimary)
            {
                var next = images
                    .Where(i => i.Id != image.Id)
                    .OrderBy(i => i.SortOrder)
                    .FirstOrDefault();

                if (next is not null)
                    next.IsPrimary = true;
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Delete bytes after commit; a file shared via dedup is kept while
            // any other record still references it.
            foreach (var file in files)
                await _storedFiles.DeletePhysicalIfOrphanAsync(file.Path, file.Url, cancellationToken);
        }
    }
}
