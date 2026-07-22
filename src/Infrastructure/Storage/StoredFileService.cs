using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Infrastructure.Storage;

public class StoredFileService : IStoredFileService
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorage _fileStorage;

    public StoredFileService(IApplicationDbContext context, IFileStorage fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    public async Task<StoredFile> CreateAsync(
        Stream content,
        string originalFileName,
        string contentType,
        DocumentType documentType,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        // IFormFile streams are forward-only, and we need the bytes twice (hash,
        // then possibly save). Uploads are capped at a few MB by the validators,
        // so buffering in memory is safe.
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        // Per-agency dedup: the global query filter already scopes this lookup to
        // the current tenant, so identical bytes are only ever shared within one
        // agency — never across the tenant boundary.
        var existing = await _context.StoredFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Sha256 == hash, cancellationToken);

        string path;
        string url;
        if (existing is not null)
        {
            // Reuse the physical file; only a new metadata row is written.
            path = existing.Path;
            url = existing.Url;
        }
        else
        {
            using var toSave = new MemoryStream(bytes, writable: false);
            url = await _fileStorage.SaveAsync(toSave, relativePath, contentType, cancellationToken);
            path = relativePath;
        }

        var file = new StoredFile
        {
            Path = path,
            Url = url,
            OriginalFileName = originalFileName,
            MimeType = contentType,
            Size = bytes.LongLength,
            Sha256 = hash,
            DocumentType = documentType
            // AgencyId is stamped by TenantEntityInterceptor on insert.
        };

        _context.StoredFiles.Add(file);
        return file;
    }

    public async Task DeletePhysicalIfOrphanAsync(
        string path,
        string url,
        CancellationToken cancellationToken = default)
    {
        var stillReferenced = await _context.StoredFiles
            .AnyAsync(f => f.Path == path, cancellationToken);

        if (!stillReferenced)
            await _fileStorage.DeleteAsync(url, cancellationToken);
    }
}
