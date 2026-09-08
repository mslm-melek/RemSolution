using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.AgencyReport.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.AgencyReport.Queries.GetMyAgencyReportsQuery
{
    // What customers have said about this agency to the platform. Administrator
    // only, like the rest of "My agency": a complaint is about how the business
    // is run, not about a booking somebody at the counter can action.
    //
    // Reports are platform-level rows, so the AgencyId predicate below is the
    // whole of the isolation — there is no query filter behind it.
    [Authorize(Roles = Roles.AgencyAdministrator)]
    public record GetMyAgencyReportsQuery(
        AgencyReportStatus? Status = null,
        int PageNumber = 1,
        int PageSize = 20
    ) : IRequest<PaginatedList<AgencyReportDto>>;

    public class GetMyAgencyReportsQueryHandler
        : IRequestHandler<GetMyAgencyReportsQuery, PaginatedList<AgencyReportDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ITenantProvider _tenant;

        public GetMyAgencyReportsQueryHandler(
            IApplicationDbContext context, ITenantProvider tenant)
        {
            _context = context;
            _tenant = tenant;
        }

        public async Task<PaginatedList<AgencyReportDto>> Handle(
            GetMyAgencyReportsQuery request, CancellationToken cancellationToken)
        {
            if (_tenant.AgencyId is not int agencyId)
            {
                throw new ForbiddenAccessException();
            }

            var reports = _context.AgencyReports
                .AsNoTracking()
                .Where(r => r.AgencyId == agencyId);

            if (request.Status is AgencyReportStatus status)
            {
                reports = reports.Where(r => r.Status == status);
            }

            return await reports
                .OrderByDescending(r => r.SubmittedAt)
                .ThenByDescending(r => r.Id)
                .Select(r => new AgencyReportDto
                {
                    Id = r.Id,
                    AgencyId = r.AgencyId,
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

namespace RemSolution.Application.Features.AgencyReport.Queries.GetMyAgencyReportsQuery
{
    public class GetMyAgencyReportsQueryValidator : AbstractValidator<GetMyAgencyReportsQuery>
    {
        public GetMyAgencyReportsQueryValidator()
        {
            RuleFor(v => v.PageNumber).GreaterThan(0);
            RuleFor(v => v.PageSize).InclusiveBetween(1, 100);
        }
    }
}
