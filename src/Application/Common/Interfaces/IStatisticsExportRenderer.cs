using RemSolution.Application.Common.Reporting;

namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// Writes a resolved <see cref="StatisticsExport"/> out in one format. Pure and
/// synchronous, like <see cref="IRentalDocumentRenderer"/>: no database and no
/// clock — every word it prints about the figures is already on the model, and
/// only its own page chrome and number formats are its own.
/// <para>
/// One implementation per format, all registered, and the handler picks by
/// <see cref="Format"/>. A switch inside a single renderer would put two
/// unrelated writers in one class.
/// </para>
/// </summary>
public interface IStatisticsExportRenderer
{
    StatisticsExportFormat Format { get; }

    string ContentType { get; }

    /// <summary>The extension, dot included, for the report's file name.</summary>
    string FileExtension { get; }

    byte[] Render(StatisticsExport export);
}
