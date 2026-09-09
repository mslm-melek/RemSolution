namespace RemSolution.Application.Common.Reporting;

/// <summary>What an export is asked for. The extension and MIME type follow.</summary>
public enum StatisticsExportFormat
{
    /// <summary>A workbook — one sheet per table, figures as numbers.</summary>
    Xlsx = 1,

    /// <summary>A printable report, for sending on rather than reworking.</summary>
    Pdf = 2,
}

/// <summary>
/// One line of an export table. The label is already localized and formatted (a
/// month name, a plate, "Total"); the six figures are still NUMBERS.
/// <para>
/// That split is deliberate, and it is where this differs from
/// <see cref="Documents.RenderedDocument"/>: the whole point of a spreadsheet
/// export is that the accountant can sum a column, which pre-formatted strings
/// would prevent. Number and date FORMATS are the renderer's business; wording
/// and rounding are not.
/// </para>
/// </summary>
public sealed record StatisticsExportRow(
    string Label,
    int Rentings,
    int RentedDays,
    decimal Charged,
    decimal Collected,
    decimal Expenses,
    decimal Net);

/// <summary>One table of the report, with its own heading for the label column.</summary>
public sealed record StatisticsExportTable(
    string Title,
    string LabelHeader,
    IReadOnlyList<StatisticsExportRow> Rows);

/// <summary>
/// A statistics report resolved for one agency and window, ready to write out in
/// either format. Both renderers get exactly this — so the workbook and the PDF
/// cannot say different things.
/// </summary>
public sealed record StatisticsExport
{
    /// <summary>Neutral language tag; drives reading direction and font choice.</summary>
    public required string Language { get; init; }

    public required string Title { get; init; }
    public required string AgencyName { get; init; }

    /// <summary>
    /// The car filter, the window and the currency, in one printable line. The
    /// ISO-4217 code goes here rather than on each money column: at the width
    /// these columns get, "Billed (TND)" would wrap on every heading.
    /// </summary>
    public required string Subtitle { get; init; }

    /// <summary>The six numeric column headings, in the row's field order.</summary>
    public required IReadOnlyList<string> ColumnHeaders { get; init; }

    /// <summary>By period, then by car when the report covers the whole fleet.</summary>
    public required IReadOnlyList<StatisticsExportTable> Tables { get; init; }

    /// <summary>Repeated under each table, so a sheet read on its own still adds up.</summary>
    public required StatisticsExportRow Totals { get; init; }

    /// <summary>Set when the window was cut back; printed as a footnote.</summary>
    public string? Note { get; init; }

    /// <summary>
    /// File name without its extension — the renderer owns that. Built where the
    /// report is, since it names the agency and the window.
    /// </summary>
    public required string FileNameStem { get; init; }
}
