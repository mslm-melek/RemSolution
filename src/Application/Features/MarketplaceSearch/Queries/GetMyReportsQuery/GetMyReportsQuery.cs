using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.MarketplaceSearch.DTOs;
using RemSolution.Domain.Constants;

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

        public GetMyReportsQueryHandler(IApplicationDbContext context, IUser user)
        {
            _context = context;
            _user = user;
        }

        public async Task<IList<MyReportDto>> Handle(
            GetMyReportsQuery request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            return await _context.AgencyReports
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
                })
                .ToListAsync(cancellationToken);
        }
    }
}
