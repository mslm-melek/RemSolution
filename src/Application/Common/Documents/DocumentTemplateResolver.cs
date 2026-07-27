using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Common.Documents;

/// <summary>
/// The outcome of resolving a template against one booking.
/// </summary>
/// <param name="Blocks">The blocks with every placeholder substituted.</param>
/// <param name="MissingRequired">
/// Ask-each-time placeholders marked required that the caller did not supply. A
/// non-empty list means the caller must prompt and retry — generating anyway would
/// put a hole in a legal document.
/// </param>
public sealed record DocumentTemplateResolution(
    IReadOnlyList<DocumentBlock> Blocks,
    IReadOnlyList<string> MissingRequired);

/// <summary>
/// Fills a template's blocks in. Pure and synchronous — no database, no clock —
/// which is what makes the binding rules testable on their own.
/// </summary>
public static class DocumentTemplateResolver
{
    /// <summary>
    /// What a placeholder resolves to, in precedence order:
    /// <list type="number">
    /// <item>its <see cref="DocumentTemplateField"/> binding, if the template has one;</item>
    /// <item>otherwise, if the name is itself a known data path, that value —
    /// this is what makes a template written with <c>{{client.fullName}}</c> work
    /// before anyone opens the binding screen;</item>
    /// <item>otherwise a blank, because an unbound name has no source.</item>
    /// </list>
    /// </summary>
    public static DocumentTemplateResolution Resolve(
        IReadOnlyList<DocumentBlock> blocks,
        IEnumerable<DocumentTemplateField> fields,
        IReadOnlyDictionary<string, string> dataValues,
        IReadOnlyDictionary<string, string>? manualValues = null)
    {
        var bindings = fields
            .GroupBy(f => f.Placeholder, StringComparer.Ordinal)
            // Defensive: the unique index makes duplicates impossible, but a
            // resolver that throws on bad data would fail a document generation
            // rather than degrade.
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var missingRequired = new List<string>();

        var resolved = blocks.Select(block => Substitute(block, Value)).ToList();

        return new DocumentTemplateResolution(resolved, missingRequired);

        string Value(string placeholder)
        {
            if (!bindings.TryGetValue(placeholder, out var field))
            {
                // Unbound: a name that happens to be a data path still works.
                return DocumentPlaceholders.IsKnown(placeholder) && dataValues.TryGetValue(placeholder, out var auto)
                    ? auto
                    : string.Empty;
            }

            switch (field.Binding)
            {
                case DocumentFieldBinding.DataField:
                    var path = field.DataPath ?? placeholder;
                    return dataValues.TryGetValue(path, out var value) ? value : string.Empty;

                case DocumentFieldBinding.FixedValue:
                    return field.FixedValue ?? string.Empty;

                case DocumentFieldBinding.AskEachTime:
                    var supplied = manualValues is not null
                                   && manualValues.TryGetValue(placeholder, out var manual)
                                   && !string.IsNullOrWhiteSpace(manual)
                        ? manual
                        : null;

                    if (supplied is null && field.IsRequired && !missingRequired.Contains(placeholder))
                    {
                        missingRequired.Add(placeholder);
                    }

                    return supplied ?? string.Empty;

                case DocumentFieldBinding.Blank:
                    // A rule to write on. Rendered inline, so it has to be drawn
                    // with characters rather than a line element.
                    return "______________";

                default:
                    return string.Empty;
            }
        }
    }

    /// <summary>
    /// Every distinct placeholder in the blocks, paired with the template's
    /// binding for it (null when unbound). Drives the binding screen and the
    /// "what must I prompt for?" query.
    /// </summary>
    public static IReadOnlyList<(string Placeholder, DocumentTemplateField? Field)> Bindings(
        IReadOnlyList<DocumentBlock> blocks,
        IEnumerable<DocumentTemplateField> fields)
    {
        var byName = fields
            .GroupBy(f => f.Placeholder, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        return DocumentTemplateBlocks.FindPlaceholders(blocks)
            .Select(name => (name, byName.TryGetValue(name, out var field) ? field : null))
            .ToList();
    }

    private static DocumentBlock Substitute(DocumentBlock block, Func<string, string> value)
    {
        string? Text(string? text) =>
            text is null ? null : DocumentTemplateBlocks.Substitute(text, value);

        return block with
        {
            Text = Text(block.Text),
            Title = Text(block.Title),
            Fields = block.Fields?
                .Select(field => field with
                {
                    Label = DocumentTemplateBlocks.Substitute(field.Label, value),
                    Value = DocumentTemplateBlocks.Substitute(field.Value, value)
                })
                .ToList(),
            Labels = block.Labels?
                .Select(label => DocumentTemplateBlocks.Substitute(label, value))
                .ToList()
        };
    }
}
