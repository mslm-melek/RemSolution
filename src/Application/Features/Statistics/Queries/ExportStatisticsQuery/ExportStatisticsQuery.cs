using System.Globalization;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Reporting;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Statistics.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Statistics.Queries.ExportStatisticsQuery
{
    /// <summary>
    /// The statistics screen as a file to send on — a workbook for the accountant
    /// who will rework the figures, a PDF for the one who only has to read them.
    /// <para>
    /// Takes the same four filters as <c>GetStatisticsQuery</c> and is gated the
    /// same way, because it is the same report: the SPA passes on whatever the
    /// screen is showing, so the download and the screen always agree.
    /// </para>
    /// </summary>
    [Authorize(Policy = Permissions.DashboardView)]
    [RequiresFeature(FeatureFlags.Dashboard)]
    public record ExportStatisticsQuery(
        int? CarId = null,
        StatisticsGranularity Granularity = StatisticsGranularity.Month,
        DateTime? From = null,
        DateTime? To = null,
        StatisticsExportFormat Format = StatisticsExportFormat.Xlsx
    ) : IRequest<FileDownload>;

    public class ExportStatisticsQueryValidator : AbstractValidator<ExportStatisticsQuery>
    {
        public ExportStatisticsQueryValidator()
        {
            RuleFor(q => q.Granularity).IsInEnum();
            // A format with no renderer would otherwise reach the handler and fail
            // there with nothing useful to say.
            RuleFor(q => q.Format).IsInEnum();
        }
    }

    public class ExportStatisticsQueryHandler : IRequestHandler<ExportStatisticsQuery, FileDownload>
    {
        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;
        private readonly IAgencySettingsProvider _settings;
        private readonly TimeProvider _dateTime;
        private readonly ILocalizer _localizer;
        private readonly IEnumerable<IStatisticsExportRenderer> _renderers;

        public ExportStatisticsQueryHandler(
            IApplicationDbContext context, ITenantProvider tenant,
            IAgencySettingsProvider settings, TimeProvider dateTime,
            ILocalizer localizer, IEnumerable<IStatisticsExportRenderer> renderers)
        {
            _context = context;
            _tenant = tenant;
            _settings = settings;
            _dateTime = dateTime;
            _localizer = localizer;
            _renderers = renderers;
        }

        public async Task<FileDownload> Handle(
            ExportStatisticsQuery request, CancellationToken cancellationToken)
        {
            var renderer = _renderers.FirstOrDefault(r => r.Format == request.Format)
                ?? throw new NotSupportedException($"No renderer for {request.Format}.");

            var agencyId = _tenant.AgencyId ?? throw new UnauthorizedAccessException();

            var report = await StatisticsReport.BuildAsync(
                _context, _tenant, _settings, _dateTime,
                request.CarId, request.Granularity, request.From, request.To,
                cancellationToken);

            // Agency is not an ITenantEntity, so this is an explicit id lookup
            // rather than a filtered read; the id is the caller's own tenant.
            var agencyName = await _context.Agencies
                .Where(a => a.Id == agencyId)
                .Select(a => a.Name)
                .FirstOrDefaultAsync(cancellationToken);

            var export = Compose(report, agencyName);

            var bytes = renderer.Render(export);

            return new FileDownload(
                new MemoryStream(bytes),
                export.FileNameStem + renderer.FileExtension,
                renderer.ContentType);
        }

        private StatisticsExport Compose(StatisticsDto report, string? agencyName)
        {
            var culture = CultureInfo.CurrentUICulture;
            var language = Languages.Normalize(culture.Name) ?? Languages.Default;
            var monthly = report.Granularity == StatisticsGranularity.Month;

            var tables = new List<StatisticsExportTable>
            {
                new(
                    _localizer[monthly ? "Statistics.ByMonth" : "Statistics.ByYear"],
                    _localizer["Statistics.Period"],
                    report.Periods.Select(row => Line(PeriodLabel(row, monthly, culture), row)).ToList())
            };

            // Only when the report covers the fleet — filtered to one car, the
            // period table above already is that car's breakdown.
            if (report.ByCar.Count > 0)
            {
                tables.Add(new StatisticsExportTable(
                    _localizer["Statistics.ByCar"],
                    _localizer["Statistics.Car"],
                    report.ByCar.Select(row => Line(CarLabel(row), row)).ToList()));
            }

            // The window as its own first and last bucket, so the reader sees the
            // rows' own labels rather than a half-open pair ending in a month the
            // report does not contain.
            var first = report.Periods.Count > 0 ? PeriodLabel(report.Periods[0], monthly, culture) : null;
            var last = report.Periods.Count > 0
                ? PeriodLabel(report.Periods[^1], monthly, culture)
                : null;

            var subtitle = new[]
                {
                    report.CarLabel ?? _localizer["Statistics.AllCars"],
                    first == last ? first : $"{first} – {last}",
                    report.Currency
                }
                .Where(part => !string.IsNullOrWhiteSpace(part));

            return new StatisticsExport
            {
                Language = language,
                Title = _localizer["Statistics.Title"],
                AgencyName = agencyName ?? string.Empty,
                Subtitle = string.Join(" · ", subtitle),
                ColumnHeaders =
                [
                    _localizer["Statistics.Rentings"],
                    _localizer["Statistics.Days"],
                    _localizer["Statistics.Charged"],
                    _localizer["Statistics.Collected"],
                    _localizer["Statistics.Expenses"],
                    _localizer["Statistics.Net"]
                ],
                Tables = tables,
                Totals = Line(_localizer["Statistics.Total"], report.Totals),
                Note = report.Truncated ? _localizer["Statistics.Truncated"] : null,
                FileNameStem = FileNameStem(report),
            };
        }

        private static StatisticsExportRow Line(string label, StatisticsRowDto row) => new(
            label,
            row.Rentings,
            row.RentedDays,
            row.Charged?.Amount ?? 0m,
            row.Collected?.Amount ?? 0m,
            row.Expenses?.Amount ?? 0m,
            row.Net?.Amount ?? 0m);

        // The buckets are UTC midnights and are read as calendar labels, not
        // instants, so they are formatted as they are — shifting one into a local
        // zone is what would label January's row "December".
        private static string PeriodLabel(
            StatisticsRowDto row, bool monthly, CultureInfo culture)
        {
            var start = row.BucketStart;
            if (start == null) return string.Empty;

            return monthly
                ? start.Value.ToString("MMMM yyyy", culture)
                : start.Value.Year.ToString(CultureInfo.InvariantCulture);
        }

        private string CarLabel(StatisticsRowDto row) => row.CarId == null
            ? _localizer["Statistics.OtherCars"]
            : string.Join(" · ", new[] { row.Matricule, row.ModelName }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

        // ASCII, and dated rather than localized: this name crosses a
        // Content-Disposition header and lands in the accountant's mail folder
        // next to eleven other months. The agency's own name is inside the file.
        private static string FileNameStem(StatisticsDto report)
        {
            var window = report.Granularity == StatisticsGranularity.Year
                ? $"{report.From:yyyy}-{report.To.AddYears(-1):yyyy}"
                : $"{report.From:yyyy-MM}-{report.To.AddMonths(-1):yyyy-MM}";

            var plate = report.CarId == null
                ? null
                : report.Cars.FirstOrDefault(c => c.Id == report.CarId)?.Matricule;

            // A plate written in a script this strips out entirely just leaves the
            // window, which is still a usable name.
            var car = new string((plate ?? string.Empty).ToLowerInvariant()
                .Where(char.IsAsciiLetterOrDigit).ToArray());

            return car.Length > 0 ? $"statistics-{car}-{window}" : $"statistics-{window}";
        }
    }
}
