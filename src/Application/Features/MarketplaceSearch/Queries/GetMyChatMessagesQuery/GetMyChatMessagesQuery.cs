using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Chat.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyChatMessagesQuery
{
    // One of the customer's own threads, oldest first, with the same AfterId
    // cursor the agency side polls with. Cross-tenant read (the customer has no
    // tenant), narrowed to bookings linked to their marketplace account — the
    // ownership check IS the isolation here, so it is expressed in the same
    // predicate as the filter bypass and cannot be forgotten separately.
    [Authorize(Policy = Policies.CustomerOnly)]
    public record GetMyChatMessagesQuery(ChatSubjectKind Subject, int Id, int? AfterId = null)
        : IRequest<IList<ChatMessageDto>>;

    public class GetMyChatMessagesQueryHandler : IRequestHandler<GetMyChatMessagesQuery, IList<ChatMessageDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IUser _user;

        public GetMyChatMessagesQueryHandler(IApplicationDbContext context, IUser user)
        {
            _context = context;
            _user = user;
        }

        public async Task<IList<ChatMessageDto>> Handle(
            GetMyChatMessagesQuery request, CancellationToken cancellationToken)
        {
            var userId = _user.Id ?? throw new UnauthorizedAccessException();

            var messages = _context.ChatMessages
                .IgnoreQueryFilters()
                .AsNoTracking();

            var query = request.Subject == ChatSubjectKind.Reservation
                ? messages.Where(m => m.ReservationId == request.Id
                                      && m.Reservation != null
                                      && m.Reservation.Client != null
                                      && m.Reservation.Client.MarketplaceUserId == userId)
                : messages.Where(m => m.RentingId == request.Id
                                      && m.Renting != null
                                      && m.Renting.Client != null
                                      && m.Renting.Client.MarketplaceUserId == userId);

            if (request.AfterId.HasValue)
            {
                query = query.Where(m => m.Id > request.AfterId);
            }

            return await query
                .OrderBy(m => m.Id)
                .ProjectToType<ChatMessageDto>()
                .ToListAsync(cancellationToken);
        }
    }
}
