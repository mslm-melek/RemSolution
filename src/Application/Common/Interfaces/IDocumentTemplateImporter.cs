using RemSolution.Application.Common.Documents;

namespace RemSolution.Application.Common.Interfaces;

/// <summary>What an import produced.</summary>
/// <param name="Blocks">The document's text as editable blocks.</param>
/// <param name="Placeholders">
/// The <c>{{names}}</c> found in it. Usually empty: a contract an agency has been
/// printing for years has hand-typed blanks, not placeholders. Inserting those is
/// the point of the editor — the import brings the WORDS in, which is the tedious
/// part.
/// </param>
public sealed record DocumentTemplateImport(
    IReadOnlyList<DocumentBlock> Blocks,
    IReadOnlyList<string> Placeholders);

/// <summary>
/// Converts an uploaded document into template blocks. Text and structure only —
/// fonts, colours and margins are the renderer's business, so nothing about the
/// source's styling survives beyond "this line was a heading".
/// </summary>
public interface IDocumentTemplateImporter
{
    /// <summary>Whether this importer can read the given file.</summary>
    bool CanImport(string fileName, string? contentType);

    /// <summary>
    /// Reads the file. Throws
    /// <see cref="Exceptions.ValidationException"/> for a file it cannot parse —
    /// a corrupt .docx is user error, not a server fault.
    /// </summary>
    Task<DocumentTemplateImport> ImportAsync(
        Stream content, string fileName, string? contentType, CancellationToken cancellationToken = default);
}
