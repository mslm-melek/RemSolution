using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Expense.Commands.UploadExpenseFactureCommand
{
    // Attaches the supplier invoice to an expense — the deferred half of the
    // StoredFile work, so Expense.FactureFileId finally gets populated. Same
    // shape as UploadCarPhotoCommand: one file FK, re-uploading replaces the
    // previous file. Auditable because it irreversibly drops the file it
    // replaces. ISensitiveRequest: carries the raw file stream — must never be
    // destructured into logs.
    // Expense.Update permission: attaching the invoice is an edit of the expense
    // record, not a permission of its own.
    [Authorize(Policy = Permissions.ExpenseUpdate)]
    [RequiresFeature(FeatureFlags.Expenses)]
    [Auditable("UploadExpenseFacture", "Expense")]
    public record UploadExpenseFactureCommand : IRequest<string>, ISensitiveRequest
    {
        public int ExpenseId { get; init; }
        public string FileName { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
        public long Length { get; init; }
        public Stream Content { get; init; } = Stream.Null;
    }

    public class UploadExpenseFactureCommandHandler : IRequestHandler<UploadExpenseFactureCommand, string>
    {
        private readonly IApplicationDbContext _context;
        private readonly IStoredFileService _storedFiles;

        public UploadExpenseFactureCommandHandler(IApplicationDbContext context, IStoredFileService storedFiles)
        {
            _context = context;
            _storedFiles = storedFiles;
        }

        public async Task<string> Handle(UploadExpenseFactureCommand request, CancellationToken cancellationToken)
        {
            var entity = await _context.Expenses
                .FindAsync(new object[] { request.ExpenseId }, cancellationToken);

            Guard.Against.NotFound(request.ExpenseId, entity);

            var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
            var relativePath =
                $"agencies/{entity.AgencyId}/expenses/{entity.Id}/facture-{Guid.NewGuid():N}{extension}";

            var file = await _storedFiles.CreateAsync(
                request.Content, request.FileName, request.ContentType,
                DocumentType.ExpenseFacture, relativePath, cancellationToken);

            // Navigations are not lazy-loaded: read the FK, then repoint it.
            var previousFileId = entity.FactureFileId;
            entity.FactureFile = file;

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

            // Only once the new invoice is durably attached: drop the replaced
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
