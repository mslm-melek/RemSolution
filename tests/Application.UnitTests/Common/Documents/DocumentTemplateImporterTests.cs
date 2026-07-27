using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NUnit.Framework;
using RemSolution.Application.Common.Documents;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Infrastructure;
using RemSolution.Infrastructure.Documents;
using RemSolution.Infrastructure.Localization;

namespace RemSolution.Application.UnitTests.Common.Documents;

/// <summary>
/// The importer is the one place the product reads a file format it does not own,
/// so the cases that matter are the messy ones: Word splitting a sentence across
/// runs, headings carrying a French style id, HTML wrapping blocks in divs, and a
/// file that is not what its extension claims.
/// </summary>
public class DocumentTemplateImporterTests
{
    [Test]
    public async Task ReadsWordParagraphsAndHeadings()
    {
        var docx = Docx(
            Paragraph("CONTRAT DE LOCATION", style: "Titre1"),
            // Word splits a sentence at every formatting change; the importer has
            // to reassemble the runs or the text arrives in fragments.
            Paragraph(new[] { "Le locataire ", "reconnaît", " avoir reçu le véhicule." }),
            Paragraph(""),
            Paragraph("Franchise: {{franchise}}"));

        var import = await Import(docx, "contrat.docx");

        import.Blocks.Should().HaveCount(3, "the empty paragraph is dropped");
        import.Blocks[0].Type.Should().Be(DocumentBlockType.Heading);
        import.Blocks[0].Text.Should().Be("CONTRAT DE LOCATION");
        import.Blocks[1].Type.Should().Be(DocumentBlockType.Paragraph);
        import.Blocks[1].Text.Should().Be("Le locataire reconnaît avoir reçu le véhicule.");
        import.Placeholders.Should().Equal("franchise");
    }

    /// <summary>
    /// Word stores the heading style id in the authoring language, so an English
    /// build must still recognise a document written in Word fr-FR.
    /// </summary>
    [TestCase("Heading1")]
    [TestCase("Titre1")]
    [TestCase("Titre 2")]
    [TestCase("heading3")]
    public async Task RecognisesHeadingStylesInAnyAuthoringLanguage(string style)
    {
        var import = await Import(Docx(Paragraph("A TITLE", style)), "x.docx");

        import.Blocks[0].Type.Should().Be(DocumentBlockType.Heading);
    }

    [Test]
    public async Task TreatsAnUnknownStyleAsAParagraph()
    {
        var import = await Import(Docx(Paragraph("Body text", style: "Normal")), "x.docx");

        import.Blocks[0].Type.Should().Be(DocumentBlockType.Paragraph);
    }

    /// <summary>Word puts non-breaking spaces in text; Regex \s does not match them.</summary>
    [Test]
    public async Task NormalisesNonBreakingSpaces()
    {
        var import = await Import(Docx(Paragraph("90 ch et plus")), "x.docx");

        import.Blocks[0].Text.Should().Be("90 ch et plus");
        // The literals above hold real U+00A0 characters, invisible in source:
        // this asserts none survived into the block text.
        import.Blocks[0].Text.Should().NotContain(" ");
    }

    [Test]
    public void RejectsAFileThatIsNotAWordDocument()
    {
        // A .doc renamed to .docx: not a zip, so it cannot be read.
        var notAZip = Encoding.UTF8.GetBytes("this is not a zip archive");

        FluentActions.Invoking(() => Import(notAZip, "renamed.docx"))
            .Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public void RejectsAnUnsupportedExtension()
    {
        FluentActions.Invoking(() => Import(Encoding.UTF8.GetBytes("x"), "contract.pdf"))
            .Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public void RejectsAFileWithNoReadableText()
    {
        FluentActions.Invoking(() => Import(Encoding.UTF8.GetBytes("   \n\n  "), "empty.txt"))
            .Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ReadsHtmlHeadingsAndParagraphs()
    {
        var html = """
            <html><head><style>p { color: red }</style></head>
            <body>
              <div>
                <h1>RENTAL AGREEMENT</h1>
                <p>The renter, {{client.fullName}}, receives the vehicle.</p>
                <p>&amp; agrees to the terms.</p>
              </div>
              <script>alert('x')</script>
            </body></html>
            """;

        var import = await Import(Encoding.UTF8.GetBytes(html), "contract.html");

        import.Blocks.Should().HaveCount(3, "the wrapping div must not be emitted as a fourth block");
        import.Blocks[0].Type.Should().Be(DocumentBlockType.Heading);
        import.Blocks[0].Text.Should().Be("RENTAL AGREEMENT");
        import.Blocks[2].Text.Should().Be("& agrees to the terms.", "entities are decoded");
        import.Blocks.Should().NotContain(b => b.Text!.Contains("alert"), "script bodies are dropped");
        import.Blocks.Should().NotContain(b => b.Text!.Contains("color: red"), "style bodies are dropped");
        import.Placeholders.Should().Equal("client.fullName");
    }

    [Test]
    public async Task ReadsPlainTextSeparatedByBlankLines()
    {
        var text = "RENTAL AGREEMENT\n\nFirst clause of the agreement.\n\nSecond clause, {{franchise}} applies.";

        var import = await Import(Encoding.UTF8.GetBytes(text), "contract.txt");

        import.Blocks.Should().HaveCount(3);
        import.Blocks[0].Type.Should().Be(DocumentBlockType.Heading,
            "a short opening line with no sentence in it is the title");
        import.Blocks[1].Type.Should().Be(DocumentBlockType.Paragraph);
        import.Placeholders.Should().Equal("franchise");
    }

    /// <summary>
    /// A first line that is clearly prose is not a title, however short the file.
    /// </summary>
    [Test]
    public async Task DoesNotMistakeAnOpeningSentenceForATitle()
    {
        var import = await Import(Encoding.UTF8.GetBytes("This is a sentence. So is this."), "x.txt");

        import.Blocks[0].Type.Should().Be(DocumentBlockType.Paragraph);
    }

    [TestCase("a.docx", true)]
    [TestCase("a.html", true)]
    [TestCase("a.htm", true)]
    [TestCase("a.txt", true)]
    [TestCase("a.md", true)]
    [TestCase("a.pdf", false)]
    [TestCase("a.doc", false)]
    [TestCase("a", false)]
    public void ReportsWhatItCanRead(string fileName, bool expected)
    {
        Importer().CanImport(fileName, null).Should().Be(expected);
    }

    private static async Task<DocumentTemplateImport> Import(byte[] content, string fileName)
    {
        using var stream = new MemoryStream(content, writable: false);
        return await Importer().ImportAsync(stream, fileName, null);
    }

    private static DocumentTemplateImporter Importer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        return new DocumentTemplateImporter(new ResourceLocalizer(
            services.BuildServiceProvider().GetRequiredService<IStringLocalizer<SharedResource>>()));
    }

    // --- minimal .docx construction, so the tests exercise the real zip/XML path ---

    private const string WordNamespace =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private static string Paragraph(string text, string? style = null) =>
        Paragraph(new[] { text }, style);

    private static string Paragraph(IEnumerable<string> runs, string? style = null)
    {
        var properties = style is null
            ? string.Empty
            : $"<w:pPr><w:pStyle w:val=\"{style}\"/></w:pPr>";

        var content = string.Concat(runs.Select(run =>
            $"<w:r><w:t xml:space=\"preserve\">{Escape(run)}</w:t></w:r>"));

        return $"<w:p>{properties}{content}</w:p>";
    }

    private static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static byte[] Docx(params string[] paragraphs)
    {
        var document = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <w:document xmlns:w="{WordNamespace}"><w:body>{string.Concat(paragraphs)}</w:body></w:document>
            """;

        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("word/document.xml");
            using var stream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(document);
            stream.Write(bytes, 0, bytes.Length);
        }

        return buffer.ToArray();
    }
}
