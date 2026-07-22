using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Client.Commands.DeleteClientCommand
{
    [Authorize(Policy = Permissions.ClientDelete)]
    [RequiresFeature(FeatureFlags.Clients)]
    [Auditable("DeleteClient", "Client")]
    public record DeleteClientCommand(int Id) : IRequest;

    public class DeleteClientCommandHandler : IRequestHandler<DeleteClientCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly IStoredFileService _storedFiles;

        public DeleteClientCommandHandler(IApplicationDbContext context, IStoredFileService storedFiles)
        {
            _context = context;
            _storedFiles = storedFiles;
        }

        public async Task Handle(DeleteClientCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Clients
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            // Load the client's document records so they can be removed with the
            // client and their bytes cleaned up afterwards (navigations are not
            // lazy-loaded, so query by the FK ids).
            var fileIds = new[] { entity.CINFileId, entity.DrivingLicenceFileId, entity.PasseportFileId }
                .OfType<int>()
                .ToArray();

            var documents = await _context.StoredFiles
                .Where(f => fileIds.Contains(f.Id))
                .ToListAsync(cancellationToken);

            // The primary Renting.ClientId FK is ON DELETE SET NULL, but
            // SecondClientId is NO ACTION (SQL Server allows only one
            // cascading path), so second-driver references must be cleared
            // here or the delete fails with an FK violation.
            var secondRentings = await _context.Rentings
                .Where(r => r.SecondClientId == request.Id)
                .ToListAsync(cancellationToken);

            foreach (var renting in secondRentings)
                renting.SecondClientId = null;

            _context.Clients.Remove(entity);

            // Removing the client (the FK holder) and its document rows in the
            // same SaveChanges: EF deletes the dependent client before the
            // principal StoredFiles, so the Restrict FK is satisfied.
            _context.StoredFiles.RemoveRange(documents);

            await _context.SaveChangesAsync(cancellationToken);

            // The client's identity documents must not outlive the record;
            // deleting bytes after the commit means a failure here leaves orphan
            // files, never a row pointing at deleted files. A file shared via
            // dedup is kept while any other record still references it.
            foreach (var document in documents)
                await _storedFiles.DeletePhysicalIfOrphanAsync(document.Path, document.Url, cancellationToken);
        }
    }
}
