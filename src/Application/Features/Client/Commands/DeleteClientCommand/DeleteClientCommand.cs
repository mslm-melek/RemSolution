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
        private readonly IFileStorage _fileStorage;

        public DeleteClientCommandHandler(IApplicationDbContext context, IFileStorage fileStorage)
        {
            _context = context;
            _fileStorage = fileStorage;
        }

        public async Task Handle(DeleteClientCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Clients
                .FindAsync(new object[] { request.Id }, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            var documentUrls = new[]
            {
                entity.CINImageUrl,
                entity.DrivingLicenceImageUrl,
                entity.PasserportImageUrl
            };

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

            await _context.SaveChangesAsync(cancellationToken);

            // The client's identity documents must not outlive the record;
            // deleting after the commit means a failure here leaves orphan
            // files, never a row pointing at deleted files.
            foreach (var url in documentUrls)
            {
                if (!string.IsNullOrEmpty(url))
                    await _fileStorage.DeleteAsync(url, cancellationToken);
            }
        }
    }
}
