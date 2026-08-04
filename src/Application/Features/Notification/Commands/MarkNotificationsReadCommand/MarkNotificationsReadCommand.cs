using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Notification.Commands.MarkNotificationsReadCommand
{
    /// <summary>
    /// Marks the caller's own notifications read — the ones listed in
    /// <see cref="Ids"/>, or every unread one when the list is empty ("mark all
    /// read").
    /// <para>
    /// One command for both because they are the same write with a different
    /// filter, and because the recipient clause is what makes either safe: ids
    /// belonging to somebody else simply do not match, so a caller cannot read
    /// another user's inbox on their behalf. Idempotent — a row already read
    /// keeps its original instant (see <c>Notification.MarkRead</c>), which is
    /// what lets the SPA mark a visible list read on every poll tick.
    /// </para>
    /// </summary>
    [RequiresFeature(FeatureFlags.Notifications)]
    public record MarkNotificationsReadCommand(IReadOnlyCollection<int>? Ids = null) : IRequest<int>;

    public class MarkNotificationsReadCommandHandler
        : IRequestHandler<MarkNotificationsReadCommand, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly IUser _user;
        private readonly TimeProvider _timeProvider;

        public MarkNotificationsReadCommandHandler(
            IApplicationDbContext context, IUser user, TimeProvider timeProvider)
        {
            _context = context;
            _user = user;
            _timeProvider = timeProvider;
        }

        public async Task<int> Handle(
            MarkNotificationsReadCommand request, CancellationToken cancellationToken)
        {
            if (_user.Id is not string userId)
            {
                return 0;
            }

            var query = _context.Notifications
                .Where(n => n.RecipientUserId == userId && n.ReadAt == null);

            if (request.Ids is { Count: > 0 })
            {
                var ids = request.Ids.ToList();
                query = query.Where(n => ids.Contains(n.Id));
            }

            var unread = await query.ToListAsync(cancellationToken);

            if (unread.Count == 0)
            {
                return 0;
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            foreach (var notification in unread)
            {
                notification.MarkRead(now);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return unread.Count;
        }
    }
}
