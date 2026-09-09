using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Reporting;
using RemSolution.Domain.Constants;

namespace RemSolution.Infrastructure.Reporting;

/// <summary>
/// The report as a printable page — for the reader who only has to look at it.
/// Landscape, because seven columns do not fit portrait.
/// <para>
/// Font and reading direction follow the same rules as the rental paperwork; see
/// <see cref="QuestPdfRentalDocumentRenderer"/> for why Arabic asks the host for a
/// font.
/// </para>
/// </summary>
public class QuestPdfStatisticsExportRenderer : IStatisticsExportRenderer
{
    private const string DefaultFontFamily = "Lato";
    private const string ArabicFontFamily = "Arial";

    // Enough for a plate and a model, or a month name, at 9pt.
    private const float NumberColumnWidth = 78;

    private readonly ILocalizer _localizer;

    public QuestPdfStatisticsExportRenderer(ILocalizer localizer)
    {
        _localizer = localizer;
    }

    public StatisticsExportFormat Format => StatisticsExportFormat.Pdf;

    public string ContentType => "application/pdf";

    public string FileExtension => ".pdf";

    public byte[] Render(StatisticsExport export)
    {
        var culture = CultureInfo.CurrentUICulture;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(style => style
                    .FontSize(9)
                    .FontFamily(Languages.IsRightToLeft(export.Language)
                        ? ArabicFontFamily
                        : DefaultFontFamily));

                if (Languages.IsRightToLeft(export.Language))
                {
                    page.ContentFromRightToLeft();
                }

                page.Header().Element(header => Header(header, export));

                page.Content().PaddingTop(12).Column(column =>
                {
                    column.Spacing(16);

                    foreach (var table in export.Tables)
                    {
                        column.Item().Element(cell => Table(cell, export, table, culture));
                    }

                    if (!string.IsNullOrWhiteSpace(export.Note))
                    {
                        column.Item().Text(export.Note)
                              .FontSize(8).FontColor(Colors.Grey.Darken1);
                    }
                });

                page.Footer().Element(Footer);
            });
        }).GeneratePdf();
    }

    private static void Header(IContainer container, StatisticsExport export)
    {
        container.Column(column =>
        {
            column.Item().Text(text =>
            {
                if (!string.IsNullOrWhiteSpace(export.AgencyName))
                {
                    text.Span($"{export.AgencyName} — ").FontSize(14).Bold();
                }

                text.Span(export.Title).FontSize(14).Bold();
            });

            column.Item().Text(export.Subtitle).FontSize(9).FontColor(Colors.Grey.Darken1);
            column.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private static void Table(
        IContainer container, StatisticsExport export, StatisticsExportTable table,
        CultureInfo culture)
    {
        container.Column(column =>
        {
            column.Item().PaddingBottom(4).Text(table.Title).FontSize(11).Bold();

            column.Item().Table(grid =>
            {
                grid.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();

                    for (var index = 0; index < export.ColumnHeaders.Count; index++)
                    {
                        columns.ConstantColumn(NumberColumnWidth);
                    }
                });

                // Repeated on every page the table runs onto, so a column of
                // figures is never read under the wrong heading.
                grid.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text(table.LabelHeader).SemiBold();

                    foreach (var heading in export.ColumnHeaders)
                    {
                        header.Cell().Element(HeaderCell).AlignRight().Text(heading).SemiBold();
                    }
                });

                foreach (var line in table.Rows)
                {
                    Line(grid, line, culture, BodyCell);
                }

                // Repeated under each table: a page read on its own still adds up.
                Line(grid, export.Totals, culture, TotalsCell, bold: true);
            });
        });
    }

    private static void Line(
        TableDescriptor grid, StatisticsExportRow line, CultureInfo culture,
        Func<IContainer, IContainer> cell, bool bold = false)
    {
        var label = grid.Cell().Element(cell).Text(line.Label);
        if (bold) label.SemiBold();

        var figures = new[]
        {
            line.Rentings.ToString("N0", culture),
            line.RentedDays.ToString("N0", culture),
            line.Charged.ToString("N2", culture),
            line.Collected.ToString("N2", culture),
            line.Expenses.ToString("N2", culture),
            line.Net.ToString("N2", culture)
        };

        foreach (var figure in figures)
        {
            var text = grid.Cell().Element(cell).AlignRight().Text(figure);
            if (bold) text.SemiBold();
        }
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingVertical(3);

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3);

    private static IContainer TotalsCell(IContainer container) =>
        container.BorderTop(1).BorderColor(Colors.Grey.Darken1).PaddingVertical(3);

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
}
