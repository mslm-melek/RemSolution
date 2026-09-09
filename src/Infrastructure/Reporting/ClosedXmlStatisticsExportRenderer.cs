using ClosedXML.Excel;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Reporting;
using RemSolution.Domain.Constants;

namespace RemSolution.Infrastructure.Reporting;

/// <summary>
/// The report as a workbook: one sheet per table, figures written as NUMBERS so
/// the accountant who receives it can sum a column. That is the whole reason this
/// format exists next to the PDF.
/// </summary>
public class ClosedXmlStatisticsExportRenderer : IStatisticsExportRenderer
{
    private const string CountFormat = "#,##0";
    private const string MoneyFormat = "#,##0.00";

    // Wide enough for "Véhicules retirés du parc" and a plate with a model beside
    // it; the figure columns for a heading and a six-figure amount.
    private const double LabelColumnWidth = 32;
    private const double FigureColumnWidth = 14;

    // Excel's own limits on a sheet name.
    private const int MaxSheetNameLength = 31;
    private static readonly char[] ForbiddenInSheetName = [':', '\\', '/', '?', '*', '[', ']'];

    public StatisticsExportFormat Format => StatisticsExportFormat.Xlsx;

    public string ContentType =>
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public string FileExtension => ".xlsx";

    public byte[] Render(StatisticsExport export)
    {
        using var workbook = new XLWorkbook();

        for (var index = 0; index < export.Tables.Count; index++)
        {
            Sheet(workbook, export, export.Tables[index], index);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return stream.ToArray();
    }

    private static void Sheet(
        XLWorkbook workbook, StatisticsExport export, StatisticsExportTable table, int index)
    {
        var sheet = workbook.Worksheets.Add(SheetName(table.Title, index));

        if (Languages.IsRightToLeft(export.Language))
        {
            sheet.RightToLeft = true;
        }

        sheet.Cell(1, 1).Value = string.IsNullOrWhiteSpace(export.AgencyName)
            ? export.Title
            : $"{export.AgencyName} — {export.Title}";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;

        sheet.Cell(2, 1).Value = export.Subtitle;
        sheet.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;

        const int headerRow = 4;

        sheet.Cell(headerRow, 1).Value = table.LabelHeader;

        for (var column = 0; column < export.ColumnHeaders.Count; column++)
        {
            sheet.Cell(headerRow, column + 2).Value = export.ColumnHeaders[column];
        }

        var header = sheet.Range(headerRow, 1, headerRow, export.ColumnHeaders.Count + 1);
        header.Style.Font.Bold = true;
        header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        var row = headerRow + 1;

        foreach (var line in table.Rows)
        {
            Line(sheet, row++, line);
        }

        // Repeated on every sheet: a sheet mailed on its own still has to add up.
        var totals = row;
        Line(sheet, totals, export.Totals);

        var totalsRange = sheet.Range(totals, 1, totals, export.ColumnHeaders.Count + 1);
        totalsRange.Style.Font.Bold = true;
        totalsRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;

        if (!string.IsNullOrWhiteSpace(export.Note))
        {
            sheet.Cell(totals + 2, 1).Value = export.Note;
            sheet.Cell(totals + 2, 1).Style.Font.FontColor = XLColor.Gray;
            sheet.Cell(totals + 2, 1).Style.Font.FontSize = 9;
        }

        sheet.SheetView.FreezeRows(headerRow);

        // Fixed widths rather than AdjustToContents: auto-fit measures text, which
        // needs font metrics from the host, and this has to produce the same file
        // on a container with no fonts installed as it does on a dev machine.
        sheet.Column(1).Width = LabelColumnWidth;

        for (var column = 2; column <= export.ColumnHeaders.Count + 1; column++)
        {
            sheet.Column(column).Width = FigureColumnWidth;
        }
    }

    private static void Line(IXLWorksheet sheet, int row, StatisticsExportRow line)
    {
        sheet.Cell(row, 1).Value = line.Label;

        sheet.Cell(row, 2).Value = line.Rentings;
        sheet.Cell(row, 3).Value = line.RentedDays;
        sheet.Cell(row, 4).Value = line.Charged;
        sheet.Cell(row, 5).Value = line.Collected;
        sheet.Cell(row, 6).Value = line.Expenses;
        sheet.Cell(row, 7).Value = line.Net;

        sheet.Range(row, 2, row, 3).Style.NumberFormat.Format = CountFormat;
        sheet.Range(row, 4, row, 7).Style.NumberFormat.Format = MoneyFormat;
    }

    // A localized table title can be longer than Excel allows and can hold
    // characters it refuses; the index keeps two trimmed titles from colliding.
    private static string SheetName(string title, int index)
    {
        var cleaned = new string(title.Where(c => !ForbiddenInSheetName.Contains(c)).ToArray()).Trim();

        if (cleaned.Length == 0) cleaned = $"Sheet{index + 1}";

        return cleaned.Length <= MaxSheetNameLength
            ? cleaned
            : cleaned[..(MaxSheetNameLength - 2)] + $"~{index + 1}";
    }
}
