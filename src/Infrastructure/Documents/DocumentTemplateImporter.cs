using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentValidation.Results;
using RemSolution.Application.Common.Documents;
using RemSolution.Application.Common.Interfaces;
using ValidationException = RemSolution.Application.Common.Exceptions.ValidationException;

namespace RemSolution.Infrastructure.Documents;

/// <summary>
/// Reads a .docx, .html or .txt into template blocks.
/// <para>
/// .docx is parsed directly: the format is a zip whose <c>word/document.xml</c>
/// holds paragraphs (<c>w:p</c>) built from text runs (<c>w:t</c>), with the style
/// id in <c>w:pStyle</c>. Extracting text and heading level needs nothing more than
/// <see cref="ZipArchive"/> and <see cref="XDocument"/>, so this deliberately does
/// NOT take an OpenXML dependency — that would be a large library pulled in to read
/// two element names.
/// </para>
/// <para>
/// What is deliberately dropped: fonts, colours, margins, images. The renderer owns
/// presentation, so preserving source styling would only produce templates that look
/// unlike every other document the product prints. Table cells come through as
/// paragraphs, which is lossy — the admin re-shapes them into a Fields block in the
/// editor.
/// </para>
/// </summary>
public class DocumentTemplateImporter : IDocumentTemplateImporter
{
    // OpenXML WordprocessingML.
    private static readonly XNamespace W =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private static readonly string[] WordExtensions = { ".docx" };
    private static readonly string[] HtmlExtensions = { ".html", ".htm" };
    private static readonly string[] TextExtensions = { ".txt", ".md" };

    // A heading in Word is a paragraph whose style is Heading1..9. Word stores the
    // style id in the AUTHORING language, so the French and German ids are matched
    // too: an imported rental contract is far likelier to come from Word fr-FR than
    // from en-US.
    private static readonly Regex HeadingStyle =
        new(@"^(heading|titre|title|Überschrift)\s*[1-9]?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Two passes, not one alternation including div: a regex match consumes its
    // whole span, so matching a wrapping <div> would swallow the <p> elements inside
    // it and they would never be seen. Leaf blocks are matched first; div is only
    // tried for markup that has none.
    private static readonly Regex HtmlLeafBlock =
        new(@"<(p|li|h[1-6])[^>]*>(.*?)</\1>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex HtmlDivBlock =
        new(@"<div[^>]*>(.*?)</div>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex HtmlTag = new("<[^>]+>", RegexOptions.Compiled);

    private static readonly Regex ScriptOrStyle =
        new(@"<(script|style)[^>]*>.*?</\1>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private static readonly Regex BlankLine = new(@"\n\s*\n", RegexOptions.Compiled);

    // Word puts one of these between a number and its unit, and Regex \s does not
    // match it — so it survives Collapse() and turns up as a stray glyph on the PDF.
    private const char NonBreakingSpace = ' ';

    private readonly ILocalizer _localizer;

    public DocumentTemplateImporter(ILocalizer localizer)
    {
        _localizer = localizer;
    }

    public bool CanImport(string fileName, string? contentType) =>
        Extensions.Contains(Extension(fileName));

    public async Task<DocumentTemplateImport> ImportAsync(
        Stream content, string fileName, string? contentType, CancellationToken cancellationToken = default)
    {
        var extension = Extension(fileName);

        if (!Extensions.Contains(extension))
        {
            throw Invalid("Validation.Document.ImportUnsupported");
        }

        // Buffered because the .docx path needs random access (the zip central
        // directory) and an uploaded stream is forward-only. Import size is bounded
        // by the command validator, so holding one in memory is safe.
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var blocks = WordExtensions.Contains(extension) ? ReadDocx(buffer)
                   : HtmlExtensions.Contains(extension) ? ReadHtml(ReadAllText(buffer))
                   : ReadPlainText(ReadAllText(buffer));

        if (blocks.Count == 0)
        {
            throw Invalid("Validation.Document.ImportEmpty");
        }

        return new DocumentTemplateImport(blocks, DocumentTemplateBlocks.FindPlaceholders(blocks));
    }

    private static IEnumerable<string> Extensions =>
        WordExtensions.Concat(HtmlExtensions).Concat(TextExtensions);

    private List<DocumentBlock> ReadDocx(Stream content)
    {
        XDocument document;

        try
        {
            using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);

            var entry = archive.GetEntry("word/document.xml")
                ?? throw Invalid("Validation.Document.ImportNotAWordFile");

            using var entryStream = entry.Open();
            document = XDocument.Load(entryStream);
        }
        catch (InvalidDataException)
        {
            // Not a zip at all — a .doc renamed to .docx, most likely.
            throw Invalid("Validation.Document.ImportNotAWordFile");
        }
        catch (System.Xml.XmlException)
        {
            // A zip with a corrupt document.xml. Still the user's bad file, so a
            // 400 rather than an unhandled 500.
            throw Invalid("Validation.Document.ImportNotAWordFile");
        }

        var blocks = new List<DocumentBlock>();

        foreach (var paragraph in document.Descendants(W + "p"))
        {
            // Runs split at every formatting change, so one sentence can be a dozen
            // w:t elements; concatenating them is what reassembles the line.
            var text = Collapse(string.Concat(paragraph.Descendants(W + "t").Select(t => t.Value)));

            if (text.Length == 0)
            {
                continue;
            }

            var style = paragraph
                .Element(W + "pPr")?
                .Element(W + "pStyle")?
                .Attribute(W + "val")?.Value;

            blocks.Add(Block(text, isHeading: HeadingStyle.IsMatch(style ?? string.Empty)));
        }

        return blocks;
    }

    private static List<DocumentBlock> ReadHtml(string html)
    {
        // Script and style bodies are code, not prose — dropped first so they never
        // surface as paragraphs.
        html = ScriptOrStyle.Replace(html, string.Empty);

        var blocks = new List<DocumentBlock>();

        foreach (Match match in HtmlLeafBlock.Matches(html))
        {
            var tag = match.Groups[1].Value.ToLowerInvariant();
            var text = Text(match.Groups[2].Value);

            if (text.Length > 0)
            {
                blocks.Add(Block(text, isHeading: tag.Length == 2 && tag[0] == 'h'));
            }
        }

        if (blocks.Count > 0)
        {
            return blocks;
        }

        // Markup that lays paragraphs out with divs instead of <p>. Nested divs
        // would produce overlapping matches, so only the innermost are taken.
        foreach (Match match in HtmlDivBlock.Matches(html))
        {
            var inner = match.Groups[1].Value;

            if (inner.Contains("<div", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = Text(inner);

            if (text.Length > 0)
            {
                blocks.Add(Block(text, isHeading: false));
            }
        }

        // A file with no block tags at all is still worth importing as prose.
        return blocks.Count > 0 ? blocks : ReadPlainText(Text(html));
    }

    // Tags out, entities decoded, whitespace collapsed.
    private static string Text(string html) =>
        Collapse(WebUtility.HtmlDecode(HtmlTag.Replace(html, " ")));

    private static List<DocumentBlock> ReadPlainText(string text)
    {
        // Blank-line separated: how people actually write plain-text contracts.
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');

        var chunks = BlankLine.Split(normalized)
            .Select(Collapse)
            .Where(chunk => chunk.Length > 0)
            .ToList();

        return chunks
            // A short opening line with no sentence in it is almost always the
            // document's title.
            .Select((chunk, index) => Block(
                chunk, isHeading: index == 0 && chunk.Length <= 80 && !chunk.Contains('.')))
            .ToList();
    }

    private static DocumentBlock Block(string text, bool isHeading) => new()
    {
        Type = isHeading ? DocumentBlockType.Heading : DocumentBlockType.Paragraph,
        Text = text
    };

    private static string ReadAllText(Stream content)
    {
        // detectEncodingFromByteOrderMarks: an exported .txt/.html is as likely to
        // be UTF-16 with a BOM as UTF-8, and misreading it produces mojibake in the
        // middle of a legal clause.
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    // Word and HTML both scatter newlines and runs of spaces through text that is
    // logically one line. Non-breaking spaces are normalised too: \s does not match
    // U+00A0, and Word puts one between a number and its unit.
    private static string Collapse(string text) =>
        Whitespace.Replace(text.Replace(NonBreakingSpace, ' '), " ").Trim();

    private static string Extension(string fileName) =>
        Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();

    private ValidationException Invalid(string messageKey) =>
        new(new[] { new ValidationFailure("File", _localizer[messageKey]) });
}
