using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Statistics.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Statistics.Queries.GetStatisticsQuery
{
    /// <summary>
    /// The agency's rental and money figures, month by month or year by year, for
    /// the whole fleet or for one car.
    /// <para>
    /// Gated exactly like the dashboard, and for the same reason: this is a
    /// read-only overview crossing the renting, payment and expense modules, so one
    /// "may see the overview screens" permission decides it rather than three
    /// per-module checks that would leave a user with a table of half-figures.
    /// </para>
    /// </summary>
    [Authorize(Policy = Permissions.DashboardView)]
    [RequiresFeature(FeatureFlags.Dashboard)]
    public record GetStatisticsQuery(
        // One car, or the whole fleet when null. The car list and a car's own page
        // link here with it set.
        int? CarId = null,
        StatisticsGranularity Granularity = StatisticsGranularity.Month,
        // Half-open [From, To), widened to whole buckets. Defaults to the current
        // calendar year by month, and to the last five years by year.
        DateTime? From = null,
        DateTime? To = null
    ) : IRequest<StatisticsDto>;

    public class GetStatisticsQueryHandler : IRequestHandler<GetStatisticsQuery, StatisticsDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;
        private readonly IAgencySettingsProvider _settings;
        private readonly TimeProvider _dateTime;

        public GetStatisticsQueryHandler(
            IApplicationDbContext context, ITenantProvider tenant,
            IAgencySettingsProvider settings, TimeProvider dateTime)
        {
            _context = context;
            _tenant = tenant;
            _settings = settings;
            _dateTime = dateTime;
        }

        // Every figure, every window rule and the attribution note live in
        // StatisticsReport — the export reads the same fold.
        public Task<StatisticsDto> Handle(
            GetStatisticsQuery request, CancellationToken cancellationToken) =>
            StatisticsReport.BuildAsync(
                _context, _tenant, _settings, _dateTime,
                request.CarId, request.Granularity, request.From, request.To,
                cancellationToken);
    }
}
