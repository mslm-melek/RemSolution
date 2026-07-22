using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Car.Commands.DeleteCarCommand
{

    [Authorize(Policy = Permissions.CarDelete)]
    [RequiresFeature(FeatureFlags.Cars)]
    [Auditable("DeleteCar", "Car")]
    public record DeleteCarCommand(int Id) : IRequest;

    public class DeleteCarCommandHandler : IRequestHandler<DeleteCarCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IStoredFileService _storedFiles;

        public DeleteCarCommandHandler(IApplicationDbContext context, IStoredFileService storedFiles)
        {
            _context = context;
            _storedFiles = storedFiles;
        }

        public async Task Handle(DeleteCarCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Cars
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            // Load the photo record (if any) so it is removed with the car and
            // its bytes cleaned up afterwards; navigations are not lazy-loaded.
            var photo = entity.PhotoFileId is int photoId
                ? await _context.StoredFiles.FirstOrDefaultAsync(f => f.Id == photoId, cancellationToken)
                : null;

            _context.Cars.Remove(entity);

            if (photo is not null)
                _context.StoredFiles.Remove(photo);

            await _context.SaveChangesAsync(cancellationToken);

            // Delete bytes after commit; a file shared via dedup is kept while
            // any other record still references it.
            if (photo is not null)
                await _storedFiles.DeletePhysicalIfOrphanAsync(photo.Path, photo.Url, cancellationToken);
        }

    }
}
