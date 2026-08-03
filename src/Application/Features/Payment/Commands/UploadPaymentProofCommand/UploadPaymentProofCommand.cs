using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Payment.Commands.UploadPaymentProofCommand
{
    // Attaches the proof of a payment — the receipt, transfer slip or supplier
    // invoice behind the entry — and points the payment at the resulting
    // StoredFile. Returns the public URL. Same shape as UploadCarPhotoCommand:
    // one file FK, re-uploading replaces the previous file. Auditable because it
    // irreversibly drops the file it replaces. ISensitiveRequest: carries the raw
    // file stream — must never be destructured into logs.
    // Payment.Update permission: attaching the proof is an edit of the payment
    // entry, not a permission of its own (the entry itself stays immutable).
    [Authorize(Policy = Permissions.PaymentUpdate)]
    [RequiresFeature(FeatureFlags.Payments)]
    [Auditable("UploadPaymentProof", "Payment")]
    public record UploadPaymentProofCommand : IRequest<string>, ISensitiveRequest
    {
        public int PaymentId { get; init; }
        public string FileName { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
        public long Length { get; init; }
        public Stream Content { get; init; } = Stream.Null;
    }

    public class UploadPaymentProofCommandHandler : IRequestHandler<UploadPaymentProofCommand, string>
    {
        private readonly IApplicationDbContext _context;
        private readonly IStoredFileService _storedFiles;

        public UploadPaymentProofCommandHandler(IApplicationDbContext context, IStoredFileService storedFiles)
        {
            _context = context;
            _storedFiles = storedFiles;
        }

        public async Task<string> Handle(UploadPaymentProofCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Payments
                .FindAsync(new object[] { request.PaymentId }, cancellationToken);

            Guard.Against.NotFound(request.PaymentId, entity);

            var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
            var relativePath =
                $"agencies/{entity.AgencyId}/payments/{entity.Id}/proof-{Guid.NewGuid():N}{extension}";

            var file = await _storedFiles.CreateAsync(
                request.Content, request.FileName, request.ContentType,
                DocumentType.PaymentProof, relativePath, cancellationToken);

            // Navigations are not lazy-loaded: read the FK, then repoint it.
            var previousFileId = entity.ProofFileId;
            entity.ProofFile = file;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // The row change did not commit: remove the just-written file so a
                // rejected upload leaves nothing publicly served.
                await _storedFiles.DeletePhysicalIfOrphanAsync(file.Path, file.Url, CancellationToken.None);
                throw;
            }

            // Only once the new proof is durably attached: drop the replaced
            // record, then its bytes if nothing else references them.
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
