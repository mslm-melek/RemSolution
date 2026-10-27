using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using RemSolution.Infrastructure.Storage;

namespace RemSolution.Web.Infrastructure;

/// <summary>
/// Where the uploads are, read from <see cref="FileStorageOptions"/> once. The
/// rate limiter, the gate and the static-file server share this one instance, so
/// they all mean the same file.
/// </summary>
public sealed class UploadedFiles : IDisposable
{
    private readonly PhysicalFileProvider _provider;

    public UploadedFiles(IOptions<FileStorageOptions> options, IWebHostEnvironment environment)
    {
        var storage = options.Value;

        BasePath = new PathString(storage.PublicBasePath.TrimEnd('/'));

        // An empty prefix would put the whole site behind the uploads branch,
        // and switch off wwwroot with it.
        if (!BasePath.HasValue)
            throw new InvalidOperationException(
                $"FileStorage:PublicBasePath must name a folder such as \"/uploads\", not \"{storage.PublicBasePath}\".");

        var root = Path.IsPathRooted(storage.RootPath)
            ? Path.GetFullPath(storage.RootPath)
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, storage.RootPath));

        Directory.CreateDirectory(root);

        _provider = new PhysicalFileProvider(root);
    }

    /// <summary>The URL prefix, e.g. "/uploads".</summary>
    public PathString BasePath { get; }

    public IFileProvider Files => _provider;

    /// <summary>
    /// A car photo that is actually on disk. The limiter waves these through, so
    /// a made-up car path must not count as one: otherwise anybody could make
    /// unlimited requests just by naming a car photo that does not exist.
    /// </summary>
    public bool IsPublicFile(PathString requestPath) =>
        requestPath.StartsWithSegments(BasePath, out var remainder) &&
        UploadPath.TryParse(remainder.Value, out var path) &&
        path.IsPublic &&
        _provider.GetFileInfo(path.RelativePath).Exists;

    public void Dispose() => _provider.Dispose();
}
