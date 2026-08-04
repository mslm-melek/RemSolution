using System.Globalization;
using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Notifications;
using RemSolution.Application.Common.Security;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Features.Notification.Commands.SendClientLateNoticeCommand
{
    /// <summary>
    /// Writes to a client to tell them their car is overdue. Sent by hand from the
    /// client list or the client's page — never on a schedule, because whether a
    /// late return is worth a letter is a judgement about that customer, and an
    /// automatic dunning mail on day one costs an agency goodwill it cannot get
    /// back.
    /// <para>
    /// Being deliberate is also why this ignores the agency's "email clients"
    /// switch: that setting governs the automatic reminders. A staff member with
    /// <see cref="Permissions.NotificationSend"/> pressing this button has made
    /// the decision the switch exists to defer.
    /// </para>
    /// </summary>
    /// <param name="ClientId">Who to write to.</param>
    /// <param name="RentingId">
    /// Which overdue hire to write about. Optional: a list row has no booking in
    /// hand, so leaving it null lets the handler pick the most overdue one.
    /// </param>
    [Authorize(Policy = Permissions.NotificationSend)]
    [RequiresFeature(FeatureFlags.Notifications)]
    [Auditable("SendClientLateNotice", "Client")]
    public record SendClientLateNoticeCommand(int ClientId, int? RentingId = null)
        : IRequest<LateNoticeResult>;

    /// <summary>What came of the attempt, for the screen to report plainly.</summary>
    /// <param name="Outcome">The delivery outcome.</param>
    /// <param name="RentingId">The hire actually written about, when one was found.</param>
    public record LateNoticeResult(ClientNotificationOutcome Outcome, int? RentingId);

    public class SendClientLateNoticeCommandHandler
        : IRequestHandler<SendClientLateNoticeCommand, LateNoticeResult>
    {
        private readonly IApplicationDbContext _context;
        private readonly INotificationService _notifications;
        private readonly IIdentityService _identity;
        private readonly IUser _user;
        private readonly TimeProvider _timeProvider;

        public SendClientLateNoticeCommandHandler(
            IApplicationDbContext context,
            INotificationService notifications,
            IIdentityService identity,
            IUser user,
            TimeProvider timeProvider)
        {
            _context = context;
            _notifications = notifications;
            _identity = identity;
            _user = user;
            _timeProvider = timeProvider;
        }

        public async Task<LateNoticeResult> Handle(
            SendClientLateNoticeCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Tenant-filtered: another agency's client reads as absent.
            var client = await _context.Clients
                .AsNoTracking()
                .Where(c => c.Id == request.ClientId)
                .Select(c => new { c.Id, c.Email, c.MarketplaceUserId })
                .FirstOrDefaultAsync(cancellationToken);

            Guard.Against.NotFound(request.ClientId, client);

            var overdue = _context.Rentings
                .AsNoTracking()
                .Where(r => r.ClientId == request.ClientId
                            && r.RentingState == RentingState.InProgress
                            && r.EndDate != null
                            && r.EndDate < now);

            if (request.RentingId is int rentingId)
            {
                overdue = overdue.Where(r => r.Id == rentingId);
            }

            var late = await overdue
                // Without an explicit booking, the most overdue one is the one to
                // write about: it is both the oldest complaint and the one the
                // client will recognise. Ordering rather than refusing to choose,
                // because a client with two cars out late still needs the letter.
                .OrderBy(r => r.EndDate)
                .Select(r => new
                {
                    r.Id,
                    r.EndDate,
                    Matricule = r.Car != null ? r.Car.Matricule : null,
                    ModelName = r.Car != null && r.Car.Model != null ? r.Car.Model.Name : null,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (late is null)
            {
                return new LateNoticeResult(ClientNotificationOutcome.NothingToSend, null);
            }

            var days = Math.Abs((int)Math.Round((now.Date - late.EndDate!.Value.Date).TotalDays));

            var args = new NotificationArgs()
                .Set("car", CarLabel(late.ModelName, late.Matricule))
                .Set("days", days)
                .SetDate("endDate", late.EndDate);

            // A client with a portal account has told us what language they read;
            // asked through IIdentityService because the Identity store is not the
            // Application layer's to query.
            var language = client!.MarketplaceUserId is string userId
                ? await _identity.GetPreferredLanguageAsync(userId, cancellationToken)
                : null;

            var result = await _notifications.NotifyClientAsync(
                new ClientNotification(
                    NotificationKind.RentingLateNotice,
                    NotificationMessages.ClientRentingLateNotice,
                    client.Id,
                    client.Email,
                    language,
                    NotificationSubject.Renting,
                    late.Id,
                    args,
                    // One per booking per day: a double-clicked button sends once,
                    // while an agency chasing a car for a week can write daily.
                    DedupToken: now.ToString(NotificationArgs.IsoDateFormat, CultureInfo.InvariantCulture),
                    SentByUserId: _user.Id,
                    IgnoreClientEmailSetting: true),
                cancellationToken);

            return new LateNoticeResult(result.Outcome, late.Id);
        }

        private static string CarLabel(string? modelName, string? matricule)
        {
            var label = string.Join(" — ", new[] { modelName, matricule }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

            return string.IsNullOrWhiteSpace(label) ? "—" : label;
        }
    }
}

namespace RemSolution.Application.Features.Notification.Commands.SendClientLateNoticeCommand
{
    public class SendClientLateNoticeCommandValidator : AbstractValidator<SendClientLateNoticeCommand>
    {
        public SendClientLateNoticeCommandValidator()
        {
            RuleFor(v => v.ClientId).GreaterThan(0);
            RuleFor(v => v.RentingId).GreaterThan(0).When(v => v.RentingId.HasValue);
        }
    }
}
