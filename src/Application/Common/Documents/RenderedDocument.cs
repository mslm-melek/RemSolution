namespace RemSolution.Application.Common.Documents;

/// <summary>A label/value pair the renderer prints verbatim.</summary>
public sealed record RenderedLineItem(string Label, string Value);

/// <summary>
/// A template resolved against one booking, ready to draw: the blocks with every
/// <c>{{placeholder}}</c> already substituted, plus the invoice rows the template
/// could not supply because they come from the data.
/// <para>
/// Every value here is already a formatted STRING. Culture, currency and date
/// formatting are decided once, where the data is read, so the renderer stays a
/// pure drawing step with no opinion about locales — and a rendered document is
/// reproducible from this record alone.
/// </para>
/// </summary>
public sealed record RenderedDocument
{
    /// <summary>Neutral language tag; drives reading direction and font choice.</summary>
    public required string Language { get; init; }

    public required IReadOnlyList<DocumentBlock> Blocks { get; init; }

    /// <summary>
    /// Rows for <see cref="DocumentBlockType.LineItems"/> blocks: the rental
    /// charge followed by one row per extra service. Empty for a contract.
    /// </summary>
    public IReadOnlyList<RenderedLineItem> LineItems { get; init; } = Array.Empty<RenderedLineItem>();

    /// <summary>
    /// Summary rows printed under the line-items table when the block asks for
    /// them (total, already paid, balance due).
    /// </summary>
    public IReadOnlyList<RenderedLineItem> Totals { get; init; } = Array.Empty<RenderedLineItem>();
}
