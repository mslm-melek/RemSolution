using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.AgencyReport.DTOs;
using RemSolution.Application.Features.MarketplaceSearch.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyReportsQuery
{
    // The signed-in customer's complaints across all agencies, newest first.
    // Scoped by the identity that raised them, not by a client row: a report is
    // the person's, and it outlives the agency archiving their client record.
    //
    // Reports are platform-level, so this reads them straight — no filter bypass,
    // unlike the bookings they point at.
    [Authorize(Policy = Policies.CustomerOnly)]
    public record GetMyReportsQuery : IRequest<IList<MyReportDto>>;

    public class GetMyReportsQueryHandler : IRequestHandler<GetMyReportsQuery, IList<MyReportDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IUser _user;
        private readonly TimeProvider _dateTime;

        public GetMyReportsQueryHandler(IApplicationDbContext context, IUser user, TimeProvider dateTime)
        {
            _context = context;
            _user = user;
            _dateTime = dateTime;
        }

        public async Task<IList<MyReportDto>> Handle(
            GetMyReportsQuery request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();
            var now = _dateTime.GetUtcNow().UtcDateTime;

            var reports = await _context.AgencyReports
                .AsNoTracking()
                .Where(r => r.ReporterUserId == userId)
                .OrderByDescending(r => r.SubmittedAt)
                .ThenByDescending(r => r.Id)
                .Select(r => new MyReportDto
                {
                    Id = r.Id,
                    AgencyId = r.AgencyId,
                    AgencyName = r.Agency != null ? r.Agency.Name : null,
                    ReservationId = r.ReservationId,
                    RentingId = r.RentingId,
                    BookingSummary = r.BookingSummary,
                    Kind = r.Kind,
                    Message = r.Message,
                    Status = r.Status,
                    SubmittedAt = r.SubmittedAt,
                    ResolvedAt = r.ResolvedAt,
                    ResolutionNote = r.ResolutionNote,
                    // AgencyReport.CanReopen, inline so EF can translate it.
                    CanReopen = r.Status == AgencyReportStatus.Withdrawn
                                && r.ReopenedAt == null
                                && r.ResolvedAt != null
                                && r.ResolvedAt.Value.AddDays(Domain.Entities.AgencyReport.ReportingWindowDays) >= now,
                })
                .ToListAsync(cancellationToken);

            var threads = await AgencyReportThreads.LoadAsync(
                _context, reports.Select(r => r.Id).ToList(), userId, cancellationToken);

            foreach (var report in reports)
            {
                report.Messages = threads[report.Id].ToList();
                report.LastMessageFromPlatform = AgencyReportThreads.LastWriterIsPlatform(report.Messages);
            }

            return reports;
        }
    }
}
