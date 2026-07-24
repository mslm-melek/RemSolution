namespace RemSolution.Application.Common.Interfaces;

// Files live outside SQL — local disk under wwwroot/uploads in dev, blob
// storage in production. Entities persist only the URL this returns.
public interface IFileStorage
{
    // relativePath is the storage-relative location, forward-slash separated
    // (e.g. "agencies/1/clients/3/cin-....jpg"). Returns the public URL to
    // persist on the entity.
    Task<string> SaveAsync(Stream content, string relativePath, string contentType, CancellationToken cancellationToken = default);

    // Opens the stored bytes at a URL previously returned by SaveAsync for
    // reading (e.g. the image pipeline re-reads an original to resize it). The
    // returned stream is the caller's to dispose. Throws if the URL is not one
    // this storage issued or the bytes are missing.
    Task<Stream> OpenReadAsync(string url, CancellationToken cancellationToken = default);

    // Accepts a URL previously returned by SaveAsync; URLs not owned by this
    // storage (e.g. external URIs) are left untouched.
    Task DeleteAsync(string url, CancellationToken cancellationToken = default);
}
