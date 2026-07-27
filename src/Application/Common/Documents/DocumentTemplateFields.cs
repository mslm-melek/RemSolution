using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Common.Documents;

/// <summary>
/// Keeps a template's binding rows in step with the placeholders its blocks
/// actually use — the "auto-map what we recognise, ask about the rest" rule the
/// binding screen shows.
/// </summary>
public static class DocumentTemplateFields
{
    /// <summary>
    /// Returns the binding rows a template should have: every placeholder the
    /// blocks use, with the caller's explicit binding where it supplied one and an
    /// automatic one otherwise.
    /// <para>
    /// Automatic means: a placeholder whose name is itself a known data path binds
    /// to that path (so a template written with <c>{{client.fullName}}</c> works
    /// with no setup at all), and anything else becomes ask-each-time — the honest
    /// default, since the system has no idea where "franchise" comes from.
    /// </para>
    /// <para>
    /// Rows for placeholders NO LONGER in the blocks are kept, not pruned: an admin
    /// mid-edit who has temporarily deleted a block should not silently lose its
    /// binding. Nothing downstream is confused by them — the resolver only asks
    /// about placeholders it finds, and the prompt query filters to those in use.
    /// </para>
    /// </summary>
    public static List<DocumentTemplateField> Reconcile(
        IReadOnlyList<DocumentBlock> blocks,
        IEnumerable<DocumentTemplateField> supplied,
        DocumentTemplateKind kind)
    {
        var fields = supplied
            .GroupBy(f => f.Placeholder, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        var known = new HashSet<string>(fields.Select(f => f.Placeholder), StringComparer.Ordinal);

        foreach (var placeholder in DocumentTemplateBlocks.FindPlaceholders(blocks))
        {
            if (!known.Add(placeholder))
            {
                continue;
            }

            fields.Add(AutoBind(placeholder, kind));
        }

        return fields;
    }

    /// <summary>The binding a placeholder gets when nobody has said otherwise.</summary>
    public static DocumentTemplateField AutoBind(string placeholder, DocumentTemplateKind kind) =>
        DocumentPlaceholders.IsAvailableFor(placeholder, kind)
            ? new DocumentTemplateField
            {
                Placeholder = placeholder,
                Binding = DocumentFieldBinding.DataField,
                DataPath = placeholder
            }
            : new DocumentTemplateField
            {
                Placeholder = placeholder,
                Binding = DocumentFieldBinding.AskEachTime,
                Label = placeholder
            };
}
