using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Chat.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Chat.Queries.GetChatThreadsQuery
{
    // The agency's chat inbox. Lists every booking the desk can talk on — the
    // ongoing and upcoming hires, and the holds it has confirmed, whether or not
    // anything has been said yet so staff can open the conversation first — plus
    // any closed booking that still has history to read back.
    [Authorize(Policy = Permissions.ChatView)]
    [RequiresFeature(FeatureFlags.Chat)]
    public record GetChatThreadsQuery(
        int PageNumber = 1,
        int PageSize = 20,
        bool OnlyUnread = false
    ) : IRequest<PaginatedList<ChatThreadDto>>;

    public class GetChatThreadsQueryHandler : IRequestHandler<GetChatThreadsQuery, PaginatedList<ChatThreadDto>>
    {
        // Enough of the last message to recognise the thread in a list row.
        private const int PreviewLength = 120;

        private readonly IApplicationDbContext _context;

        public GetChatThreadsQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedList<ChatThreadDto>> Handle(
            GetChatThreadsQuery request, CancellationToken cancellationToken)
        {
            var rentings = _context.Rentings
                .AsNoTracking()
                .Where(r => r.RentingState == RentingState.NotYet
                            || r.RentingState == RentingState.InProgress
                            || r.ChatMessages!.Any());

            // A hold is listed once the agency has committed to it — the same
            // rule the send path enforces (ChatMessage.CanPostTo) — or once it
            // carries history, so a conversation held before a hold was cancelled
            // stays readable.
            var reservations = _context.Reservations
                .AsNoTracking()
                .Where(r => r.Status == ReservationStatus.Confirmed
                            || r.Status == ReservationStatus.Paid
                            || r.ChatMessages!.Any());

            if (request.OnlyUnread)
            {
                rentings = rentings.Where(r => r.ChatMessages!
                    .Any(m => m.AuthorKind == ChatAuthorKind.Client && m.ReadAt == null));

                reservations = reservations.Where(r => r.ChatMessages!
                    .Any(m => m.AuthorKind == ChatAuthorKind.Client && m.ReadAt == null));
            }

            var rentingThreads = rentings.Select(r => new ChatThreadDto
            {
                Subject = ChatSubjectKind.Renting,
                RentingId = r.Id,
                ReservationId = null,
                CarId = r.CarId,
                CarMatricule = r.Car != null ? r.Car.Matricule : null,
                ClientId = r.ClientId,
                ClientName = r.Client != null
                    ? ((r.Client.FirstName ?? string.Empty) + " " + (r.Client.LastName ?? string.Empty)).Trim()
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
                UnreadCount = r.ChatMessages!
                    .Count(m => m.AuthorKind == ChatAuthorKind.Client && m.ReadAt == null),
                IsOpen = r.RentingState == RentingState.NotYet
                         || r.RentingState == RentingState.InProgress,
            });

            var reservationThreads = reservations.Select(r => new ChatThreadDto
            {
                Subject = ChatSubjectKind.Reservation,
                RentingId = null,
                ReservationId = r.Id,
                CarId = r.CarId,
                CarMatricule = r.Car != null ? r.Car.Matricule : null,
                ClientId = r.ClientId,
                ClientName = r.Client != null
                    ? ((r.Client.FirstName ?? string.Empty) + " " + (r.Client.LastName ?? string.Empty)).Trim()
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
                    .Count(m => m.AuthorKind == ChatAuthorKind.Client && m.ReadAt == null),
                IsOpen = r.Status == ReservationStatus.Confirmed
                         || r.Status == ReservationStatus.Paid,
            });

            // One list, both kinds: to the desk this is a set of conversations,
            // not two inboxes. Concat renders as UNION ALL, so the ordering and
            // the paging below still happen in the database.
            return await rentingThreads
                .Concat(reservationThreads)
                // Most recent conversation first; bookings with nothing said yet
                // (NULL last message) fall to the end of the list.
                .OrderByDescending(t => t.LastMessageAt)
                .ThenByDescending(t => t.StartDate)
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}
