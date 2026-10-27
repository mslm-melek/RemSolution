using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using RemSolution.Application.Common.Files;
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
    /// The uploaded file this request names, if it names one that is actually on
    /// disk; null for any other request. Looked up once per request and kept on
    /// it, so the rate limiter and the gate ask the disk once between them.
    /// </summary>
    public UploadPath? FindExisting(HttpContext context)
    {
        if (context.Items.TryGetValue(LookupKey, out var found))
            return (UploadPath?)found;

        // Inside the uploads branch PathBase holds the prefix; outside, Path does.
        var requestPath = context.Request.PathBase.Add(context.Request.Path);

        UploadPath? existing =
            requestPath.StartsWithSegments(BasePath, out var remainder) &&
            UploadPath.TryParse(remainder.Value, out var path) &&
            _provider.GetFileInfo(path.RelativePath).Exists
                ? path
                : null;

        context.Items[LookupKey] = existing;
        return existing;
    }

    private static readonly object LookupKey = new();

    public void Dispose() => _provider.Dispose();
}
