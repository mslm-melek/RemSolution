using System.Globalization;

namespace RemSolution.Application.Common.Files;

/// <summary>
/// A path under the uploads prefix, read the way the gate reads it:
/// <c>agencies/{agencyId}/{area}/…</c>. Shared by the gate, the rate limiter and
/// the file store, so all three agree on what an area is and which one is public.
///
/// <para>
/// Parsing is strict on purpose. The decision is taken on these segments, but
/// the file is then opened by the operating system, which reads more into a path
/// than '/' does: on Windows '\' is a separator too (Kestrel decodes %5C into
/// one), '..' climbs, ':' names an alternate data stream, a trailing dot or
/// space is dropped. A path that could mean something different to the disk
/// than to this parser is refused outright.
/// </para>
/// </summary>
public readonly record struct UploadPath(int AgencyId, string Area, string RelativePath)
{
    /// <summary>The one area anyone may open: the marketplace shows car photos to everybody.</summary>
    public const string PublicArea = "cars";

    public bool IsPublic => Area.Equals(PublicArea, StringComparison.OrdinalIgnoreCase);

    /// <summary>The folder the area's access rule covers, e.g. "agencies/4/cars/".</summary>
    public string AreaPrefix => string.Join('/', RelativePath.Split('/').Take(3)) + "/";

    /// <param name="remainder">The path after the uploads prefix, e.g. "/agencies/4/cars/12/photo.jpg".</param>
    public static bool TryParse(string? remainder, out UploadPath path)
    {
        path = default;

        if (string.IsNullOrEmpty(remainder) || remainder.IndexOfAny(['\\', ':', '\0']) >= 0)
            return false;

        var segments = remainder.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Windows trims a trailing dot or space from a name ("..." included), so
        // a segment shaped like that names something other than it spells.
        if (segments.Length < 4 ||
            segments.Any(s => s.EndsWith('.') || s.EndsWith(' ') || s.StartsWith(' ')) ||
            !segments[0].Equals("agencies", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(segments[1], NumberStyles.None, CultureInfo.InvariantCulture, out var agencyId))
        {
            return false;
        }

        path = new UploadPath(agencyId, segments[2], string.Join('/', segments));
        return true;
    }
}
