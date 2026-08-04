using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Mappings;
using RemSolution.Application.Common.Models;
using RemSolution.Application.Common.Notifications;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Notification.DTOs;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Notification.Queries.GetMyNotificationsQuery
{
    /// <summary>
    /// The caller's own inbox, newest first.
    /// <para>
    /// Gated on the feature but on no permission: an inbox is not a module, and
    /// every row in it was addressed to this user by
    /// <c>INotificationService</c>, which already applied the permission rule
    /// when it chose the recipients. Filtering by recipient here is therefore the
    /// whole access control — there is no parameter that could widen it.
    /// </para>
    /// </summary>
    [RequiresFeature(FeatureFlags.Notifications)]
    public record GetMyNotificationsQuery(
        int PageNumber = 1,
        int PageSize = 20,
        bool OnlyUnread = false,
        NotificationKind? Kind = null
    ) : IRequest<PaginatedList<NotificationDto>>;

    public class GetMyNotificationsQueryHandler
        : IRequestHandler<GetMyNotificationsQuery, PaginatedList<NotificationDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IUser _user;

        public GetMyNotificationsQueryHandler(IApplicationDbContext context, IUser user)
        {
            _context = context;
            _user = user;
        }

        public async Task<PaginatedList<NotificationDto>> Handle(
            GetMyNotificationsQuery request, CancellationToken cancellationToken)
        {
            if (_user.Id is not string userId)
            {
                return new PaginatedList<NotificationDto>(
                    Array.Empty<NotificationDto>(), 0, request.PageNumber, request.PageSize);
            }

            // Tenant-filtered on top of the recipient match. Rows with no
            // recipient are the record of mail sent to a client and belong to
            // nobody's inbox, so the equality below excludes them.
            var query = _context.Notifications
                .AsNoTracking()
                .Where(n => n.RecipientUserId == userId);

            if (request.OnlyUnread)
            {
                query = query.Where(n => n.ReadAt == null);
            }

            if (request.Kind.HasValue)
            {
                query = query.Where(n => n.Kind == request.Kind);
            }

            // Projected to a flat row first: the arguments are stored as JSON and
            // are unpacked below, which SQL cannot do. Paginating before that
            // keeps it to one page's worth of parsing.
            var page = await query
                .OrderByDescending(n => n.CreatedAt)
                .ThenByDescending(n => n.Id)
                .Select(n => new Row(
                    n.Id, n.Kind, n.MessageKey, n.ArgsJson,
                    n.SubjectType, n.SubjectId, n.Link, n.CreatedAt, n.ReadAt))
                .PaginatedListAsync(request.PageNumber, request.PageSize, cancellationToken);

            var items = page.Items
                .Select(row => new NotificationDto
                {
                    Id = row.Id,
                    Kind = row.Kind,
                    MessageKey = row.MessageKey,
                    Args = NotificationArgs.FromJson(row.ArgsJson)
                        .ToDictionary(pair => pair.Key, pair => pair.Value),
                    SubjectType = row.SubjectType,
                    SubjectId = row.SubjectId,
                    Link = row.Link,
                    CreatedAt = row.CreatedAt,
                    ReadAt = row.ReadAt,
                })
                .ToList();

            return new PaginatedList<NotificationDto>(
                items, page.TotalCount, page.PageNumber, request.PageSize);
        }

        // Shape EF materializes, before the JSON arguments are unpacked. A class
        // rather than an anonymous type only because PaginatedListAsync needs a
        // named reference type.
        private sealed record Row(
            int Id,
            NotificationKind Kind,
            string MessageKey,
            string? ArgsJson,
            NotificationSubject SubjectType,
            int? SubjectId,
            string? Link,
            DateTime CreatedAt,
            DateTime? ReadAt);
    }
}
