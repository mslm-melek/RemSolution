using RemSolution.Application.Common.Documents;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.DocumentTemplate.DTOs
{
    /// <summary>
    /// A shipped example, for the "start from one of these" picker.
    /// <para>
    /// <see cref="Key"/> is NOT a database id — examples are code, not rows (see
    /// <see cref="DocumentTemplateExamples"/>) — so cloning takes the key, not an id.
    /// </para>
    /// </summary>
    public class DocumentTemplateExampleDto
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public DocumentTemplateKind Kind { get; init; }
        public string Language { get; init; } = string.Empty;

        /// <summary>Rendered in the preview pane before the admin commits to a clone.</summary>
        public IList<DocumentBlock> Blocks { get; init; } = new List<DocumentBlock>();
    }

    /// <summary>
    /// A value a template can pull from a booking — one entry per
    /// <see cref="RemSolution.Domain.Constants.DocumentPlaceholders"/> path. Drives
    /// the editor's "insert a field" picker and the binding dropdown.
    /// </summary>
    public class DocumentPlaceholderDto
    {
        /// <summary>The dotted path, e.g. "client.fullName".</summary>
        public string Path { get; init; } = string.Empty;

        /// <summary>The token to paste into a block, e.g. "{{client.fullName}}".</summary>
        public string Token { get; init; } = string.Empty;

        /// <summary>Leading segment ("client", "car", "renting"), for grouping the picker.</summary>
        public string Group { get; init; } = string.Empty;
    }
}
