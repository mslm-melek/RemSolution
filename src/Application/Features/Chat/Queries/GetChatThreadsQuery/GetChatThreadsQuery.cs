using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Chat.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Chat.Queries.GetChatThreadsQuery
{
    // The agency's chat inbox. Lists every renting the desk can talk on — the
    // ongoing and upcoming ones, whether or not anything has been said yet, so
    // staff can open the conversation first — plus any closed renting that still
    // has history to read back.
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
            var query = _context.Rentings
                .AsNoTracking()
                .Where(r => r.RentingState == RentingState.NotYet
                            || r.RentingState == RentingState.InProgress
                            || r.ChatMessages!.Any());

            if (request.OnlyUnread)
            {
                query = query.Where(r => r.ChatMessages!
                    .Any(m => m.AuthorKind == ChatAuthorKind.Client && m.ReadAt == null));
            }

            return await query
                // Most recent conversation first; rentings with nothing said yet
                // (NULL last message) fall to the end of the list.
                .OrderByDescending(r => r.ChatMessages!.Max(m => (DateTime?)m.SentAt))
                .ThenByDescending(r => r.StartDate)
                .Select(r => new ChatThreadDto
                {
                    RentingId = r.Id,
                    CarId = r.CarId,
                    CarMatricule = r.Car != null ? r.Car.Matricule : null,
                    ClientId = r.ClientId,
                    ClientName = r.Client != null
                        ? ((r.Client.FirstName ?? string.Empty) + " " + (r.Client.LastName ?? string.Empty)).Trim()
                        : null,
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                    RentingState = r.RentingState,
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
                })
                .PaginatedListAsync(request.PageNumber, request.PageSize);
        }
    }
}
