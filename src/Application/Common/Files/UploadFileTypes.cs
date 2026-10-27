namespace RemSolution.Application.Common.Files;

/// <summary>
/// The only file types the app accepts, and the only ones the uploads gate serves
/// (Program.cs maps exactly <see cref="All"/>). One list, so a type is never
/// validated but unservable, or servable but never validated.
/// </summary>
public static class UploadFileTypes
{
    /// <summary>The largest file any upload accepts. Uploads are buffered in memory to be hashed.</summary>
    public const long MaxSizeBytes = 15 * 1024 * 1024;

    public static readonly IReadOnlyDictionary<string, string> All =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp",
            [".pdf"] = "application/pdf",
        };

    // A scan or a PDF: identity papers, payment proofs, invoices, a customer's answer.
    public static readonly string[] DocumentExtensions = All.Keys.ToArray();

    public static readonly string[] DocumentContentTypes = All.Values.Distinct().ToArray();

    // A car photo is an image only: the marketplace shows it inline.
    public static readonly string[] ImageExtensions =
        All.Where(t => t.Value.StartsWith("image/", StringComparison.Ordinal)).Select(t => t.Key).ToArray();

    public static readonly string[] ImageContentTypes =
        All.Values.Where(v => v.StartsWith("image/", StringComparison.Ordinal)).Distinct().ToArray();
}
