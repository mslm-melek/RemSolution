using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Chat.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Chat.Queries.GetChatMessagesQuery
{
    // One thread, oldest first. The SPA polls this while a thread is open, so it
    // passes AfterId to fetch only what arrived since the last message it holds —
    // an id cursor rather than a timestamp, which cannot skip two messages that
    // share a clock tick.
    [Authorize(Policy = Permissions.ChatView)]
    [RequiresFeature(FeatureFlags.Chat)]
    public record GetChatMessagesQuery(int RentingId, int? AfterId = null)
        : IRequest<IList<ChatMessageDto>>;

    public class GetChatMessagesQueryHandler : IRequestHandler<GetChatMessagesQuery, IList<ChatMessageDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetChatMessagesQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IList<ChatMessageDto>> Handle(
            GetChatMessagesQuery request, CancellationToken cancellationToken)
        {
            // Tenant-filtered: another agency's thread comes back empty.
            var query = _context.ChatMessages
                .AsNoTracking()
                .Where(m => m.RentingId == request.RentingId);

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
