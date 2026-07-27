using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NUnit.Framework;
using QuestPDF.Infrastructure;
using RemSolution.Application.Common.Documents;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;
using RemSolution.Infrastructure;
using RemSolution.Infrastructure.Documents;
using RemSolution.Infrastructure.Localization;

namespace RemSolution.Application.UnitTests.Common.Documents;

/// <summary>
/// The renderer is the one piece of the document pipeline with no database in it,
/// so it is worth pinning here: that every block type draws at all (a layout
/// mistake throws at render time, not at compile time), that the shipped example
/// templates render in every shipped language — including the right-to-left one,
/// which flips the layout and asks the host for a different font — and that the
/// optional blocks disappear rather than leaving empty boxes.
/// </summary>
public class QuestPdfRentalDocumentRendererTests
{
    [OneTimeSetUp]
    public void ConfigureQuestPdf()
    {
        // Production sets these in AddInfrastructureServices; a unit test never
        // builds that graph. Without the licence QuestPDF throws on first render,
        // and with glyph checking on, Arabic fails wherever no Arabic font is
        // installed — see the comments at both call sites.
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
    }

    [TestCase(Languages.English)]
    [TestCase(Languages.French)]
    [TestCase(Languages.Arabic)]
    public void RendersTheStandardContractExample(string language)
    {
        var pdf = Render(Examples().Default(DocumentTemplateKind.Contract, language).Blocks, language);

        pdf.Should().StartWith(PdfMagic, "the output must be a real PDF, not an error page or empty buffer");
        // A one-page A4 document with this much content is a few KB; a couple of
        // hundred bytes would mean the content block silently rendered nothing.
        pdf.Length.Should().BeGreaterThan(1000);
    }

    [TestCase(Languages.English)]
    [TestCase(Languages.French)]
    [TestCase(Languages.Arabic)]
    public void RendersTheStandardInvoiceExample(string language)
    {
        var blocks = Examples().Default(DocumentTemplateKind.Facture, language).Blocks;

        var pdf = Render(blocks, language, lineItems: new[]
        {
            new RenderedLineItem("Location du véhicule", "480,00 TND"),
            new RenderedLineItem("Siège bébé", "40,00 TND")
        }, totals: new[]
        {
            new RenderedLineItem("Total", "520,00 TND"),
            new RenderedLineItem("Reste à payer", "220,00 TND")
        });

        pdf.Should().StartWith(PdfMagic);
        pdf.Length.Should().BeGreaterThan(1000);
    }

    /// <summary>
    /// Every block type in one document: a layout error in a rarely-used block
    /// would otherwise only surface on a customer's template.
    /// </summary>
    [Test]
    public void RendersEveryBlockType()
    {
        var blocks = new List<DocumentBlock>
        {
            new() { Type = DocumentBlockType.Heading, Text = "TITLE" },
            new() { Type = DocumentBlockType.Paragraph, Text = "A clause." },
            new() { Type = DocumentBlockType.Paragraph, Text = "Fine print.", Fine = true },
            new()
            {
                Type = DocumentBlockType.Fields,
                SideBySide = true,
                Title = "Left",
                Fields = new List<DocumentBlockField> { new() { Label = "A", Value = "1" } }
            },
            new()
            {
                Type = DocumentBlockType.Fields,
                SideBySide = true,
                Title = "Right",
                Fields = new List<DocumentBlockField> { new() { Label = "B", Value = "2" } }
            },
            new() { Type = DocumentBlockType.Spacer, Height = 20 },
            new() { Type = DocumentBlockType.LineItems, ShowTotals = true },
            new() { Type = DocumentBlockType.PageBreak },
            new() { Type = DocumentBlockType.Signatures, Labels = new List<string> { "One", "Two", "Three" } },
        };

        var pdf = Render(blocks, Languages.French,
            lineItems: new[] { new RenderedLineItem("Row", "1,00 TND") },
            totals: new[] { new RenderedLineItem("Total", "1,00 TND") });

        pdf.Should().StartWith(PdfMagic);
    }

    /// <summary>
    /// The empty cases every real booking hits: no second driver, no deposit, no
    /// notes. Each is an optional block or row, and a null reaching the layout
    /// throws rather than printing a blank.
    /// </summary>
    [Test]
    public void RendersWithEveryOptionalBlockEmpty()
    {
        var blocks = new List<DocumentBlock>
        {
            new() { Type = DocumentBlockType.Heading, Text = "TITLE" },
            // Placeholders that resolved to nothing.
            new() { Type = DocumentBlockType.Paragraph, Text = "   " },
            new()
            {
                Type = DocumentBlockType.Fields,
                Title = "All hidden",
                Fields = new List<DocumentBlockField>
                {
                    new() { Label = "A", Value = "", HideWhenEmpty = true },
                    new() { Label = "B", Value = "", HideWhenEmpty = true }
                }
            },
            // Not hidden: a deliberate blank to complete by hand.
            new()
            {
                Type = DocumentBlockType.Fields,
                Title = "Blank to fill",
                Fields = new List<DocumentBlockField> { new() { Label = "Franchise", Value = "" } }
            },
            new() { Type = DocumentBlockType.LineItems, ShowTotals = true },
            new() { Type = DocumentBlockType.Signatures, Labels = new List<string>() },
        };

        Render(blocks, Languages.French).Should().StartWith(PdfMagic);
    }

    /// <summary>A template with nothing in it must not crash the generator.</summary>
    [Test]
    public void RendersAnEmptyTemplate()
    {
        Render(new List<DocumentBlock>(), Languages.French).Should().StartWith(PdfMagic);
    }

    private static readonly byte[] PdfMagic = Encoding.ASCII.GetBytes("%PDF");

    private static byte[] Render(
        IReadOnlyList<DocumentBlock> blocks,
        string language,
        IReadOnlyList<RenderedLineItem>? lineItems = null,
        IReadOnlyList<RenderedLineItem>? totals = null)
    {
        return new QuestPdfRentalDocumentRenderer(Localizer()).Render(new RenderedDocument
        {
            Language = language,
            Blocks = blocks,
            LineItems = lineItems ?? Array.Empty<RenderedLineItem>(),
            Totals = totals ?? Array.Empty<RenderedLineItem>()
        });
    }

    private static DocumentTemplateExamples Examples() => new(Localizer());

    private static ResourceLocalizer Localizer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        return new ResourceLocalizer(
            services.BuildServiceProvider().GetRequiredService<IStringLocalizer<SharedResource>>());
    }
}
