using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Imaging;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Client.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Client.Commands.RegenerateClientPortraitCommand
{
    // Re-cuts the client's portrait out of the CIN image already on file.
    //
    // The portrait is normally produced by the upload itself (see
    // UploadClientDocumentCommand), so this exists for the two cases that upload
    // cannot cover: clients whose CIN was stored before portraits existed, and an
    // image the automatic crop got wrong or gave up on. Cheap enough to offer as
    // a button — it re-reads one image and writes one small JPEG.
    //
    // Client.Update: the portrait is part of the client record, and it replaces
    // a stored file irreversibly, so it is audited like the upload.
    [Authorize(Policy = Permissions.ClientUpdate)]
    [RequiresFeature(FeatureFlags.Clients)]
    [Auditable("RegenerateClientPortrait", "Client")]
    public record RegenerateClientPortraitCommand(int Id) : IRequest<ClientPortraitDto>;

    public class RegenerateClientPortraitCommandHandler
        : IRequestHandler<RegenerateClientPortraitCommand, ClientPortraitDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly IStoredFileService _storedFiles;
        private readonly ClientPortraitFactory _portraits;

        public RegenerateClientPortraitCommandHandler(
            IApplicationDbContext context,
            IStoredFileService storedFiles,
            ClientPortraitFactory portraits)
        {
            _context = context;
            _storedFiles = storedFiles;
            _portraits = portraits;
        }

        public async Task<ClientPortraitDto> Handle(
            RegenerateClientPortraitCommand request, CancellationToken cancellationToken)
        {
            // Tracked, with the CIN file loaded: the factory reads its URL back out
            // of storage, and the FK swap below is a normal tracked update.
            var entity = await _context.Clients
                .Include(c => c.CINFile)
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

            Guard.Against.NotFound(request.Id, entity);

            if (entity.CINFile is null)
            {
                return new ClientPortraitDto { HasCinImage = false };
            }

            var previousPortraitId = entity.CINPortraitFileId;
            var portrait = await _portraits.TryCreateFromStoredCinAsync(entity, cancellationToken);

            if (portrait is null)
            {
                // Nothing found. The portrait already on file, if any, was cut from
                // this same image, so it is no worse than what we just failed to
                // produce — keep it rather than blanking the client's face.
                return new ClientPortraitDto
                {
                    HasCinImage = true,
                    PortraitUrl = entity.CINPortraitFile?.Url
                };
            }

            entity.CINPortraitFile = portrait;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                await _storedFiles.DeletePhysicalIfOrphanAsync(
                    portrait.Path, portrait.Url, CancellationToken.None);
                throw;
            }

            // Only once the new portrait is durably attached: drop the superseded
            // record, then its bytes if nothing else references them (a re-crop of
            // an unchanged image dedups to the same physical file, which the
            // orphan check keeps).
            if (previousPortraitId is int previousId)
            {
                var previous = await _context.StoredFiles
                    .FirstOrDefaultAsync(f => f.Id == previousId, cancellationToken);

                if (previous is not null)
                {
                    _context.StoredFiles.Remove(previous);
                    await _context.SaveChangesAsync(cancellationToken);
                    await _storedFiles.DeletePhysicalIfOrphanAsync(
                        previous.Path, previous.Url, cancellationToken);
                }
            }

            return new ClientPortraitDto { HasCinImage = true, PortraitUrl = portrait.Url };
        }
    }
}
