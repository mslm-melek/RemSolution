using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Notification.Queries.GetMyUnreadNotificationCountQuery
{
    /// <summary>
    /// What the bell in the navigation bar shows. Its own endpoint rather than a
    /// field on the list: the SPA polls this on a timer from every screen, and it
    /// is a covered COUNT against the filtered index, not a page of rows.
    /// </summary>
    [RequiresFeature(FeatureFlags.Notifications)]
    public record GetMyUnreadNotificationCountQuery : IRequest<int>;

    public class GetMyUnreadNotificationCountQueryHandler
        : IRequestHandler<GetMyUnreadNotificationCountQuery, int>
    {
        private readonly IApplicationDbContext _context;
        private readonly IUser _user;
        private readonly ITenantProvider _tenant;

        public GetMyUnreadNotificationCountQueryHandler(
            IApplicationDbContext context, IUser user, ITenantProvider tenant)
        {
            _context = context;
            _user = user;
            _tenant = tenant;
        }

        public async Task<int> Handle(
            GetMyUnreadNotificationCountQuery request, CancellationToken cancellationToken)
        {
            if (_user.Id is not string userId)
            {
                return 0;
            }

            // No agency: the platform inbox (see GetMyNotificationsQuery).
            if (_tenant.AgencyId is null)
            {
                return await _context.PlatformNotifications
                    .AsNoTracking()
                    .CountAsync(n => n.RecipientUserId == userId && n.ReadAt == null, cancellationToken);
            }

            return await _context.Notifications
                .AsNoTracking()
                .CountAsync(n => n.RecipientUserId == userId && n.ReadAt == null, cancellationToken);
        }
    }
}
