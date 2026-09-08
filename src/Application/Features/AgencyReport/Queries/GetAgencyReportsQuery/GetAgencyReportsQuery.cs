using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.AgencyReport.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.AgencyReport.Queries.GetAgencyReportsQuery
{
    /// <summary>
    /// The platform's triage queue. Open first and oldest first by default: a
    /// complaint waiting three weeks is the one that has cost the platform its
    /// credibility, not the one that arrived this morning.
    /// <para>
    /// No tenant, no bypass — reports are platform-level rows (see
    /// <c>AgencyReport</c>), which is what lets a platform administrator read
    /// every agency's without going through the audited cross-tenant path.
    /// </para>
    /// </summary>
    [Authorize(Policy = Policies.PlatformAdminOnly)]
    public record GetAgencyReportsQuery(
        AgencyReportStatus? Status = null,
        int? AgencyId = null,
        int PageNumber = 1,
        int PageSize = 20
    ) : IRequest<PaginatedList<AgencyReportDto>>;

    public class GetAgencyReportsQueryHandler
        : IRequestHandler<GetAgencyReportsQuery, PaginatedList<AgencyReportDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetAgencyReportsQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedList<AgencyReportDto>> Handle(
            GetAgencyReportsQuery request, CancellationToken cancellationToken)
        {
            var reports = _context.AgencyReports.AsNoTracking();

            if (request.Status is AgencyReportStatus status)
            {
                reports = reports.Where(r => r.Status == status);
            }

            if (request.AgencyId is int agencyId)
            {
                reports = reports.Where(r => r.AgencyId == agencyId);
            }

            return await reports
                // Open before settled, then oldest first within each: the queue
                // is a to-do list, and a settled report is history.
                .OrderBy(r => r.Status == AgencyReportStatus.Open ? 0 : 1)
                .ThenBy(r => r.SubmittedAt)
                .ThenBy(r => r.Id)
                .Select(r => new AgencyReportDto
                {
                    Id = r.Id,
                    AgencyId = r.AgencyId,
                    AgencyName = r.Agency != null ? r.Agency.Name : null,
                    ReservationId = r.ReservationId,
                    RentingId = r.RentingId,
                    ReporterName = r.ReporterName,
                    BookingSummary = r.BookingSummary,
                    AgencyCancellationReason = r.AgencyCancellationReason,
                    Kind = r.Kind,
                    Message = r.Message,
                    Status = r.Status,
                    SubmittedAt = r.SubmittedAt,
                    ResolvedAt = r.ResolvedAt,
                    ResolutionNote = r.ResolutionNote,
                })
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}

namespace RemSolution.Application.Features.AgencyReport.Queries.GetAgencyReportsQuery
{
    public class GetAgencyReportsQueryValidator : AbstractValidator<GetAgencyReportsQuery>
    {
        public GetAgencyReportsQueryValidator()
        {
            RuleFor(v => v.PageNumber).GreaterThan(0);
            RuleFor(v => v.PageSize).InclusiveBetween(1, 100);
        }
    }
}
