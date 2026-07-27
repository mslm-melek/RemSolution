using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace RemSolution.Application.Common.Documents;

/// <summary>
/// What a template is made of. A rental contract is, structurally, a title, a few
/// boxed detail grids, a run of clause paragraphs and a signature row — so the
/// block set is deliberately small and closed rather than a general-purpose
/// document model. A closed set is what lets one renderer draw every template,
/// an importer produce one, and an editor offer a fixed palette.
/// </summary>
public enum DocumentBlockType
{
    /// <summary>Large bold line — the document's title or a section heading.</summary>
    Heading = 0,

    /// <summary>A run of text; the clause paragraphs of a contract.</summary>
    Paragraph = 1,

    /// <summary>A boxed label/value grid — "Lessor", "Vehicle", "Rental period".</summary>
    Fields = 2,

    /// <summary>
    /// The invoice's billed-lines table. Its rows come from the booking (rental
    /// charge + extra services) rather than from the template, so the block only
    /// carries the column headings and the totals switch.
    /// </summary>
    LineItems = 3,

    /// <summary>One ruled line per label, for wet signatures.</summary>
    Signatures = 4,

    /// <summary>Start a new page.</summary>
    PageBreak = 5,

    /// <summary>Vertical whitespace.</summary>
    Spacer = 6
}

/// <summary>One label/value row inside a <see cref="DocumentBlockType.Fields"/> block.</summary>
public sealed record DocumentBlockField
{
    public string Label { get; init; } = string.Empty;

    /// <summary>May contain <c>{{placeholders}}</c>.</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// Drop the whole row when the value resolves to nothing. Keeps optional
    /// details (a second driver's passport, a deposit) from printing as "—".
    /// </summary>
    public bool HideWhenEmpty { get; init; }
}

/// <summary>
/// A block of a template. One record with nullable members rather than a subtype
/// per block kind: the whole thing is serialized to and from JSON and edited as a
/// flat list, and polymorphic JSON would buy nothing but ceremony here.
/// </summary>
public sealed record DocumentBlock
{
    public DocumentBlockType Type { get; init; }

    /// <summary>Heading / Paragraph text. May contain <c>{{placeholders}}</c>.</summary>
    public string? Text { get; init; }

    /// <summary>Fields / LineItems / Signatures group heading. May contain placeholders.</summary>
    public string? Title { get; init; }

    /// <summary>Fields blocks only.</summary>
    public List<DocumentBlockField>? Fields { get; init; }

    /// <summary>Signatures blocks only — one ruled line per entry.</summary>
    public List<string>? Labels { get; init; }

    /// <summary>
    /// Fields blocks: lay the rows out side by side. Two Fields blocks with
    /// <c>SideBySide</c> are drawn as one row of two boxes (lessor next to renter).
    /// </summary>
    public bool SideBySide { get; init; }

    /// <summary>Paragraph blocks: render smaller and greyer, for terms and conditions.</summary>
    public bool Fine { get; init; }

    /// <summary>Spacer blocks: height in points. Defaults to a single line.</summary>
    public double? Height { get; init; }

    /// <summary>LineItems blocks: print the total / paid / balance summary underneath.</summary>
    public bool ShowTotals { get; init; }
}

/// <summary>
/// Serialization and placeholder scanning for a template's block list. The JSON
/// shape is a persisted contract (<c>DocumentTemplate.BlocksJson</c>), so the
/// options live here rather than at each call site — a stray casing change would
/// silently orphan every stored template.
/// </summary>
public static class DocumentTemplateBlocks
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    // Matches {{ name }} and captures "name". Dots and underscores are allowed so
    // data paths ("client.fullName") and custom names ("franchise_2026") both fit;
    // anything else is not treated as a placeholder at all.
    private static readonly Regex PlaceholderPattern =
        new(@"\{\{\s*([A-Za-z][A-Za-z0-9_.]*)\s*\}\}", RegexOptions.Compiled);

    public static string Serialize(IEnumerable<DocumentBlock> blocks) =>
        JsonSerializer.Serialize(blocks.ToList(), Options);

    /// <summary>
    /// Reads a stored block list. Returns an empty list for null/blank rather than
    /// throwing: a template with no blocks is a legitimate (if useless) state, and
    /// an empty column must not break a list screen.
    /// </summary>
    public static List<DocumentBlock> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<DocumentBlock>();
        }

        return JsonSerializer.Deserialize<List<DocumentBlock>>(json, Options) ?? new List<DocumentBlock>();
    }

    /// <summary>
    /// Every distinct placeholder name used anywhere in the blocks, in first-seen
    /// order. This is what drives the binding table: one row per name, however
    /// many times it appears.
    /// </summary>
    public static List<string> FindPlaceholders(IEnumerable<DocumentBlock> blocks)
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var text in TemplatedTexts(blocks))
        {
            foreach (Match match in PlaceholderPattern.Matches(text))
            {
                var name = match.Groups[1].Value;

                if (seen.Add(name))
                {
                    found.Add(name);
                }
            }
        }

        return found;
    }

    /// <summary>Placeholders in a single string — used by the importer and validators.</summary>
    public static IEnumerable<string> FindPlaceholders(string? text) =>
        string.IsNullOrEmpty(text)
            ? Enumerable.Empty<string>()
            : PlaceholderPattern.Matches(text).Select(m => m.Groups[1].Value);

    /// <summary>
    /// Substitutes every placeholder using <paramref name="resolve"/>. An
    /// unresolved name (null from the resolver) is replaced with an empty string
    /// rather than left as literal braces — printing "{{franchise}}" on a contract
    /// handed to a customer is worse than printing nothing.
    /// </summary>
    public static string Substitute(string? text, Func<string, string?> resolve) =>
        string.IsNullOrEmpty(text)
            ? string.Empty
            : PlaceholderPattern.Replace(text, match => resolve(match.Groups[1].Value) ?? string.Empty);

    // Every string in a block that may carry placeholders.
    private static IEnumerable<string> TemplatedTexts(IEnumerable<DocumentBlock> blocks)
    {
        foreach (var block in blocks)
        {
            if (block.Text is not null) yield return block.Text;
            if (block.Title is not null) yield return block.Title;

            foreach (var field in block.Fields ?? Enumerable.Empty<DocumentBlockField>())
            {
                yield return field.Label;
                yield return field.Value;
            }

            foreach (var label in block.Labels ?? Enumerable.Empty<string>())
            {
                yield return label;
            }
        }
    }
}
