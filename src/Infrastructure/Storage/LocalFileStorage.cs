using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Infrastructure.Storage;

// Local-disk implementation for dev: files land under wwwroot/uploads and are
// served by UseStaticFiles. Production swaps in a blob-storage implementation
// behind the same IFileStorage interface.
public class LocalFileStorage : IFileStorage
{
    private readonly FileStorageOptions _options;
    private readonly IHostEnvironment _environment;

    public LocalFileStorage(IOptions<FileStorageOptions> options, IHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public async Task<string> SaveAsync(Stream content, string relativePath, string contentType, CancellationToken cancellationToken = default)
    {
        var safeRelative = Normalize(relativePath);
        var fullPath = ResolveUnderRoot(safeRelative);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = File.Create(fullPath);
        await content.CopyToAsync(file, cancellationToken);

        return $"{_options.PublicBasePath.TrimEnd('/')}/{safeRelative}";
    }

    public Task DeleteAsync(string url, CancellationToken cancellationToken = default)
    {
        var basePath = _options.PublicBasePath.TrimEnd('/') + "/";

        if (!url.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask; // not ours — leave external URIs alone

        string fullPath;
        try
        {
            fullPath = ResolveUnderRoot(Normalize(url.Substring(basePath.Length)));
        }
        catch (ArgumentException)
        {
            // Malformed or traversal-shaped: not a URL SaveAsync ever issued,
            // so there is nothing of ours to delete.
            return Task.CompletedTask;
        }

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    private string ResolveUnderRoot(string safeRelative)
    {
        // A relative RootPath must anchor to the host content root (which
        // UseStaticFiles serves from), not the process working directory —
        // services and IIS start with arbitrary CWDs.
        var root = Path.IsPathRooted(_options.RootPath)
            ? Path.GetFullPath(_options.RootPath)
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _options.RootPath));

        var fullPath = Path.GetFullPath(Path.Combine(root, safeRelative.Replace('/', Path.DirectorySeparatorChar)));

        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Storage path '{safeRelative}' escapes the storage root.");

        return fullPath;
    }

    private static string Normalize(string relativePath)
    {
        var segments = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0 || segments.Any(s => s == "." || s == ".."))
            throw new ArgumentException($"Invalid storage path '{relativePath}'.", nameof(relativePath));

        return string.Join('/', segments);
    }
}
