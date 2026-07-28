using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Chat.Commands.MarkChatReadCommand
{
    // The agency has opened a thread: stamps the client's unread messages as
    // read. Only the OTHER side's messages are touched — the agency never marks
    // its own messages read, since ReadAt means "the recipient saw it".
    // Idempotent: re-opening a thread with nothing new is a no-op.
    [Authorize(Policy = Permissions.ChatView)]
    [RequiresFeature(FeatureFlags.Chat)]
    public record MarkChatReadCommand(int RentingId) : IRequest;

    public class MarkChatReadCommandHandler : IRequestHandler<MarkChatReadCommand>
    {
        private readonly IApplicationDbContext _context;
        private readonly TimeProvider _dateTime;

        public MarkChatReadCommandHandler(IApplicationDbContext context, TimeProvider dateTime)
        {
            _context = context;
            _dateTime = dateTime;
        }

        public async Task Handle(MarkChatReadCommand request, CancellationToken cancellationToken)
        {
            // Tenant-filtered, so another agency's thread yields nothing to mark.
            var unread = await _context.ChatMessages
                .Where(m => m.RentingId == request.RentingId
                            && m.AuthorKind == ChatAuthorKind.Client
                            && m.ReadAt == null)
                .ToListAsync(cancellationToken);

            if (unread.Count == 0)
            {
                return;
            }

            var now = _dateTime.GetUtcNow().UtcDateTime;

            foreach (var message in unread)
            {
                message.ReadAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
