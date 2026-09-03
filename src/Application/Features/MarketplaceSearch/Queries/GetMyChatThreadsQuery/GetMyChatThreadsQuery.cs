using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.MarketplaceSearch.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyChatThreadsQuery
{
    // The signed-in customer's conversations across ALL agencies — cross-tenant
    // read, which is why it lives here (see TenantEnforcementTests). Scoped by
    // the Client → MarketplaceUserId link, so a customer only ever sees the
    // threads of bookings that are theirs.
    [Authorize(Policy = Policies.CustomerOnly)]
    public record GetMyChatThreadsQuery : IRequest<IList<MyChatThreadDto>>;

    public class GetMyChatThreadsQueryHandler : IRequestHandler<GetMyChatThreadsQuery, IList<MyChatThreadDto>>
    {
        private const int PreviewLength = 120;

        private readonly IApplicationDbContext _context;
        private readonly IUser _user;

        public GetMyChatThreadsQueryHandler(IApplicationDbContext context, IUser user)
        {
            _context = context;
            _user = user;
        }

        public async Task<IList<MyChatThreadDto>> Handle(
            GetMyChatThreadsQuery request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            var rentings = _context.Rentings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.Client != null && r.Client.MarketplaceUserId == userId)
                .Select(r => new MyChatThreadDto
                {
                    Subject = ChatSubjectKind.Renting,
                    RentingId = r.Id,
                    ReservationId = null,
                    AgencyId = r.AgencyId,
                    AgencyName = r.Agency != null ? r.Agency.Name : null,
                    CarMatricule = r.Car != null ? r.Car.Matricule : null,
                    CarModelName = r.Car != null && r.Car.Model != null ? r.Car.Model.Name : null,
                    CarBrandName = r.Car != null && r.Car.Model != null && r.Car.Model.Brand != null
                        ? r.Car.Model.Brand.Name
                        : null,
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                    RentingState = r.RentingState,
                    ReservationStatus = null,
                    LastMessageAt = r.ChatMessages!.Max(m => (DateTime?)m.SentAt),
                    LastMessagePreview = r.ChatMessages!
                        .OrderByDescending(m => m.Id)
                        .Select(m => m.Body.Length > PreviewLength ? m.Body.Substring(0, PreviewLength) : m.Body)
                        .FirstOrDefault(),
                    LastMessageAuthorKind = r.ChatMessages!
                        .OrderByDescending(m => m.Id)
                        .Select(m => (ChatAuthorKind?)m.AuthorKind)
                        .FirstOrDefault(),
                    // The customer's unread = what the agency sent them.
                    UnreadCount = r.ChatMessages!
                        .Count(m => m.AuthorKind == ChatAuthorKind.Agency && m.ReadAt == null),
                    IsOpen = r.RentingState == RentingState.NotYet
                             || r.RentingState == RentingState.InProgress,
                });

            // Holds the agency has confirmed, where the questions that matter
            // most are asked — what to bring, where to collect, whether the
            // transfer arrived — plus any that still carry history.
            var reservations = _context.Reservations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.Client != null && r.Client.MarketplaceUserId == userId
                            && (r.Status == ReservationStatus.Confirmed
                                || r.Status == ReservationStatus.Paid
                                || r.ChatMessages!.Any()))
                .Select(r => new MyChatThreadDto
                {
                    Subject = ChatSubjectKind.Reservation,
                    RentingId = null,
                    ReservationId = r.Id,
                    AgencyId = r.AgencyId,
                    AgencyName = r.Agency != null ? r.Agency.Name : null,
                    CarMatricule = r.Car != null ? r.Car.Matricule : null,
                    CarModelName = r.Car != null && r.Car.Model != null ? r.Car.Model.Name : null,
                    CarBrandName = r.Car != null && r.Car.Model != null && r.Car.Model.Brand != null
                        ? r.Car.Model.Brand.Name
                        : null,
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                    RentingState = null,
                    ReservationStatus = r.Status,
                    LastMessageAt = r.ChatMessages!.Max(m => (DateTime?)m.SentAt),
                    LastMessagePreview = r.ChatMessages!
                        .OrderByDescending(m => m.Id)
                        .Select(m => m.Body.Length > PreviewLength ? m.Body.Substring(0, PreviewLength) : m.Body)
                        .FirstOrDefault(),
                    LastMessageAuthorKind = r.ChatMessages!
                        .OrderByDescending(m => m.Id)
                        .Select(m => (ChatAuthorKind?)m.AuthorKind)
                        .FirstOrDefault(),
                    UnreadCount = r.ChatMessages!
                        .Count(m => m.AuthorKind == ChatAuthorKind.Agency && m.ReadAt == null),
                    IsOpen = r.Status == ReservationStatus.Confirmed
                             || r.Status == ReservationStatus.Paid,
                });

            return await rentings
                .Concat(reservations)
                .OrderByDescending(t => t.LastMessageAt)
                .ThenByDescending(t => t.StartDate)
                .ToListAsync(cancellationToken);
        }
    }
}
