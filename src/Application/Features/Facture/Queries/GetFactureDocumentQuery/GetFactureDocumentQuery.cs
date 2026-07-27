using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Facture.Queries.GetFactureDocumentQuery
{
    // See GetContractDocumentQuery for why downloads go through the API.
    [Authorize(Policy = Permissions.FactureRead)]
    [RequiresFeature(FeatureFlags.Factures)]
    public record GetFactureDocumentQuery(int Id) : IRequest<FileDownload?>;

    public class GetFactureDocumentQueryHandler : IRequestHandler<GetFactureDocumentQuery, FileDownload?>
    {
        private readonly IApplicationDbContext _context;
        private readonly IFileStorage _fileStorage;

        public GetFactureDocumentQueryHandler(IApplicationDbContext context, IFileStorage fileStorage)
        {
            _context = context;
            _fileStorage = fileStorage;
        }

        public async Task<FileDownload?> Handle(
            GetFactureDocumentQuery request, CancellationToken cancellationToken)
        {
            var document = await _context.Factures
                .AsNoTracking()
                .Where(f => f.Id == request.Id)
                .Select(f => new { f.Number, Url = f.DocumentFile!.Url, f.DocumentFile!.MimeType })
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
