using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RemSolution.Application.Common.Documents;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Constants;

namespace RemSolution.Infrastructure.Documents;

/// <summary>
/// Draws a resolved template as an A4 PDF: one block type at a time, in order.
/// <para>
/// The only things this decides for itself are page chrome (size, margins, page
/// numbers) and the line-items table's column headings — everything else comes
/// from the template. Reading direction flips for Arabic, which is also the one
/// case where Arabic glyph coverage depends on a host font: see
/// <see cref="FontFor"/>.
/// </para>
/// </summary>
public class QuestPdfRentalDocumentRenderer : IRentalDocumentRenderer
{
    // QuestPDF bundles Lato; it has no Arabic glyphs, so Arabic asks the host for
    // a font that does.
    private const string DefaultFontFamily = "Lato";
    private const string ArabicFontFamily = "Arial";

    private readonly ILocalizer _localizer;

    public QuestPdfRentalDocumentRenderer(ILocalizer localizer)
    {
        _localizer = localizer;
    }

    public byte[] Render(RenderedDocument document)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(style => style.FontSize(10).FontFamily(FontFor(document.Language)));

                if (Languages.IsRightToLeft(document.Language))
                {
                    page.ContentFromRightToLeft();
                }

                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    Blocks(column, document);
                });

                page.Footer().Element(Footer);
            });
        }).GeneratePdf();
    }

    private void Blocks(ColumnDescriptor column, RenderedDocument document)
    {
        var blocks = document.Blocks;

        for (var index = 0; index < blocks.Count; index++)
        {
            var block = blocks[index];

            // Two consecutive side-by-side Fields blocks are drawn as one row of
            // two boxes — the lessor/renter pairing every contract opens with.
            // Consumed together, so the loop skips the second.
            if (block.Type == DocumentBlockType.Fields && block.SideBySide
                && index + 1 < blocks.Count
                && blocks[index + 1].Type == DocumentBlockType.Fields && blocks[index + 1].SideBySide)
            {
                var left = block;
                var right = blocks[index + 1];
                index++;

                column.Item().Row(row =>
                {
                    row.RelativeItem().Element(cell => FieldsBlock(cell, left));
                    row.ConstantItem(20);
                    row.RelativeItem().Element(cell => FieldsBlock(cell, right));
                });

                continue;
            }

            Block(column, block, document);
        }
    }

    private void Block(ColumnDescriptor column, DocumentBlock block, RenderedDocument document)
    {
        switch (block.Type)
        {
            case DocumentBlockType.Heading:
                column.Item().Column(heading =>
                {
                    heading.Item().Text(block.Text).FontSize(16).Bold();
                    heading.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });
                break;

            case DocumentBlockType.Paragraph:
                // Blank paragraphs happen when every placeholder in them resolved
                // to nothing; printing an empty box would look like a defect.
                if (string.IsNullOrWhiteSpace(block.Text))
                {
                    break;
                }

                var paragraph = column.Item().Text(block.Text).LineHeight(1.3f);

                if (block.Fine)
                {
                    paragraph.FontSize(8).FontColor(Colors.Grey.Darken2);
                }
                break;

            case DocumentBlockType.Fields:
                // A Fields block whose every row dropped out (an absent second
                // driver, say) leaves no empty titled box behind.
                if (VisibleFields(block).Count > 0)
                {
                    column.Item().Element(cell => FieldsBlock(cell, block));
                }
                break;

            case DocumentBlockType.LineItems:
                column.Item().Element(cell => LineItemsBlock(cell, block, document));
                break;

            case DocumentBlockType.Signatures:
                column.Item().Element(cell => SignaturesBlock(cell, block));
                break;

            case DocumentBlockType.PageBreak:
                column.Item().PageBreak();
                break;

            case DocumentBlockType.Spacer:
                column.Item().Height((float)(block.Height ?? 12));
                break;
        }
    }

    private static List<DocumentBlockField> VisibleFields(DocumentBlock block) =>
        (block.Fields ?? new List<DocumentBlockField>())
            .Where(f => !f.HideWhenEmpty || !string.IsNullOrWhiteSpace(f.Value))
            .ToList();

    private static void FieldsBlock(IContainer container, DocumentBlock block)
    {
        var fields = VisibleFields(block);

        container
            .Border(1).BorderColor(Colors.Grey.Lighten2)
            .Padding(8)
            .Column(column =>
            {
                if (!string.IsNullOrWhiteSpace(block.Title))
                {
                    column.Item().PaddingBottom(4).Text(block.Title)
                          .FontSize(9).Bold().FontColor(Colors.Grey.Darken2);
                }

                column.Item().Column(rows =>
                {
                    rows.Spacing(2);

                    foreach (var field in fields)
                    {
                        rows.Item().Text(text =>
                        {
                            if (!string.IsNullOrWhiteSpace(field.Label))
                            {
                                text.Span($"{field.Label}: ").FontColor(Colors.Grey.Darken1);
                            }

                            // An empty value that was NOT hidden is a deliberate
                            // blank to complete by hand.
                            text.Span(string.IsNullOrWhiteSpace(field.Value) ? "______________" : field.Value);
                        });
                    }
                });
            });
    }

    private void LineItemsBlock(IContainer container, DocumentBlock block, RenderedDocument document)
    {
        container.Column(column =>
        {
            if (!string.IsNullOrWhiteSpace(block.Title))
            {
                column.Item().PaddingBottom(4).Text(block.Title).FontSize(9).Bold();
            }

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(110);
                });

                // Column headings are chrome, not template text: the rows they
                // head come from the booking, not from the template.
                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text(_localizer["Document.Description"]).SemiBold();
                    header.Cell().Element(HeaderCell).AlignRight()
                          .Text(_localizer["Document.Amount"]).SemiBold();
                });

                foreach (var item in document.LineItems)
                {
                    table.Cell().Element(BodyCell).Text(item.Label);
                    table.Cell().Element(BodyCell).AlignRight().Text(item.Value);
                }
            });

            if (block.ShowTotals && document.Totals.Count > 0)
            {
                column.Item().PaddingTop(6).AlignRight().Column(totals =>
                {
                    totals.Spacing(2);

                    foreach (var total in document.Totals)
                    {
                        totals.Item().Text($"{total.Label}: {total.Value}").SemiBold();
                    }
                });
            }
        });
    }

    private static void SignaturesBlock(IContainer container, DocumentBlock block)
    {
        var labels = block.Labels ?? new List<string>();

        if (labels.Count == 0)
        {
            return;
        }

        container.PaddingTop(20).Row(row =>
        {
            for (var index = 0; index < labels.Count; index++)
            {
                if (index > 0)
                {
                    row.ConstantItem(30);
                }

                var label = labels[index];
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text(label).FontSize(9).FontColor(Colors.Grey.Darken1);
                    // Blank space to sign in, closed by a rule.
                    column.Item().PaddingTop(28).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                });
            }
        });
    }

    // Invoice table cells: a ruled header row over plain, lightly separated rows.
    private static IContainer HeaderCell(IContainer container) =>
        container
            .BorderBottom(1).BorderColor(Colors.Grey.Darken1)
            .PaddingVertical(4);

    private static IContainer BodyCell(IContainer container) =>
        container
            .BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(4);

    private void Footer(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken1));
            text.Span($"{_localizer["Document.PageOf"]} ");
            text.CurrentPageNumber();
            text.Span(" / ");
            text.TotalPages();
        });
    }

    // On a machine with no Arabic-capable font the text renders as fallback boxes
    // rather than failing the request (glyph checking is turned off in
    // AddInfrastructureServices). Deploy targets that serve Arabic documents need
    // an Arabic font installed, or one registered via QuestPDF's FontManager.
    private static string FontFor(string language) =>
        Languages.IsRightToLeft(language) ? ArabicFontFamily : DefaultFontFamily;
}
