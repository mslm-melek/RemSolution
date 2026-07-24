using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Car.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Car.Commands.UploadCarImageCommand
{
    // Adds an image to a car's gallery: stores the full-resolution original via
    // IStoredFileService, creates the CarImage row (Pending), then enqueues
    // thumbnail/medium generation to run out of band. Returns the new image
    // (derivative URLs null until the pipeline completes). Car.Update: adding an
    // image is an edit of the car. ISensitiveRequest: carries the raw stream.
    [Authorize(Policy = Permissions.CarUpdate)]
    [RequiresFeature(FeatureFlags.Cars)]
    [Auditable("UploadCarImage", "Car")]
    public record UploadCarImageCommand : IRequest<CarImageDto>, ISensitiveRequest
    {
        public int CarId { get; init; }
        public string FileName { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
        public long Length { get; init; }
        public Stream Content { get; init; } = Stream.Null;
    }

    public class UploadCarImageCommandHandler : IRequestHandler<UploadCarImageCommand, CarImageDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly IStoredFileService _storedFiles;
        private readonly IImageProcessingQueue _queue;
        private readonly IMapper _mapper;

        public UploadCarImageCommandHandler(
            IApplicationDbContext context,
            IStoredFileService storedFiles,
            IImageProcessingQueue queue,
            IMapper mapper)
        {
            _context = context;
            _storedFiles = storedFiles;
            _queue = queue;
            _mapper = mapper;
        }

        public async Task<CarImageDto> Handle(UploadCarImageCommand request, CancellationToken cancellationToken)
        {
            var car = await _context.Cars
                .FindAsync(new object[] { request.CarId }, cancellationToken);

            Guard.Against.NotFound(request.CarId, car);

            var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
            var relativePath =
                $"agencies/{car.AgencyId}/cars/{car.Id}/images/original-{Guid.NewGuid():N}{extension}";

            var original = await _storedFiles.CreateAsync(
                request.Content, request.FileName, request.ContentType,
                DocumentType.CarPhoto, relativePath, cancellationToken);

            var image = new CarImage
            {
                CarId = car.Id,
                OriginalFile = original,
                ProcessingStatus = ImageProcessingStatus.Pending,
            };

            try
            {
                // Computing SortOrder/IsPrimary from the current gallery and
                // inserting must be atomic per car, or two concurrent uploads
                // could both read an empty gallery and both land primary/0. The
                // per-agency write lock serialises it (as CreateCarCommand does).
                await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
                await _context.AcquireTenantWriteLockAsync(cancellationToken);

                var existing = await _context.CarImages
                    .Where(i => i.CarId == car.Id)
                    .Select(i => (int?)i.SortOrder)
                    .ToListAsync(cancellationToken);

                // Append to the end of the gallery; the first image is primary.
                image.SortOrder = (existing.Max() ?? -1) + 1;
                image.IsPrimary = existing.Count == 0;

                _context.CarImages.Add(image);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await _storedFiles.DeletePhysicalIfOrphanAsync(original.Path, original.Url, CancellationToken.None);
                throw;
            }

            // Enqueue only after the row is committed, so the worker can find it.
            await _queue.EnqueueAsync(new ImageProcessingJob(image.Id, car.AgencyId), cancellationToken);

            return _mapper.Map<CarImageDto>(image);
        }
    }
}
