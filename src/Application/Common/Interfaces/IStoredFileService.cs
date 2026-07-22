using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Common.Interfaces;

// Owns the StoredFile lifecycle: hashing, per-agency dedup and physical-byte
// cleanup. Upload handlers go through this instead of touching IFileStorage
// directly, so the dedup rule lives in exactly one place.
public interface IStoredFileService
{
    // Buffers and SHA-256-hashes the content, dedups within the current agency
    // (identical bytes reuse an existing physical file rather than being written
    // twice), then creates and tracks a StoredFile and returns it. Does NOT call
    // SaveChanges — the caller commits it together with the owning entity's FK.
    Task<StoredFile> CreateAsync(
        Stream content,
        string originalFileName,
        string contentType,
        DocumentType documentType,
        string relativePath,
        CancellationToken cancellationToken = default);

    // Deletes the physical bytes at the given path IFF no StoredFile row in the
    // current agency still references it. Call after the referencing rows are
    // removed and committed, so a shared (deduped) file is deleted only with its
    // last reference — and a failed delete leaves an orphan file, never a row
    // pointing at deleted bytes.
    Task DeletePhysicalIfOrphanAsync(
        string path,
        string url,
        CancellationToken cancellationToken = default);
}
