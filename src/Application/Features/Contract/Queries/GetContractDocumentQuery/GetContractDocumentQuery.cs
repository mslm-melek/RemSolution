using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Contract.Queries.GetContractDocumentQuery
{
    // Serves the archived PDF through the API so the read is authorized (see
    // FileDownload). Tenant-scoped by the global query filter, so one agency can
    // never fetch another's paperwork by guessing an id.
    [Authorize(Policy = Permissions.ContractRead)]
    [RequiresFeature(FeatureFlags.Contracts)]
    public record GetContractDocumentQuery(int Id) : IRequest<FileDownload?>;

    public class GetContractDocumentQueryHandler : IRequestHandler<GetContractDocumentQuery, FileDownload?>
    {
        private readonly IApplicationDbContext _context;
        private readonly IFileStorage _fileStorage;

        public GetContractDocumentQueryHandler(IApplicationDbContext context, IFileStorage fileStorage)
        {
            _context = context;
            _fileStorage = fileStorage;
        }

        public async Task<FileDownload?> Handle(
            GetContractDocumentQuery request, CancellationToken cancellationToken)
        {
            var document = await _context.Contracts
                .AsNoTracking()
                .Where(c => c.Id == request.Id)
                .Select(c => new { c.Number, Url = c.DocumentFile!.Url, c.DocumentFile!.MimeType })
                .FirstOrDefaultAsync(cancellationToken);

            if (document is null)
            {
                return null;
            }

            var content = await _fileStorage.OpenReadAsync(document.Url, cancellationToken);

            return new FileDownload(content, $"{document.Number}.pdf", document.MimeType);
        }
    }
}
