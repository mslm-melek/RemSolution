using System.Globalization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Notifications;
using RemSolution.Application.Common.Settings;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Infrastructure.Data;
using RemSolution.Infrastructure.Email;

namespace RemSolution.Infrastructure.Notifications;

/// <summary>
/// The single write path into the notifications table. See
/// <see cref="INotificationService"/> for the contract and why it lives here.
/// </summary>
public class NotificationService : INotificationService
{
    // The concrete context, not the interface: a rejected insert has to be
    // detached or it is retried by the next save, and a sweep shares one context
    // across every agency it visits — a poisoned change tracker would take the
    // rest of the run down with it. Legitimate here; this is Infrastructure.
    private readonly ApplicationDbContext _context;
    private readonly INotificationRecipients _recipients;
    private readonly ITenantProvider _tenant;
    private readonly IAgencySettingsProvider _settings;
    private readonly IEmailSender _emailSender;
    private readonly NotificationTextRenderer _renderer;
    private readonly EmailOptions _emailOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext context,
        INotificationRecipients recipients,
        ITenantProvider tenant,
        IAgencySettingsProvider settings,
        IEmailSender emailSender,
        NotificationTextRenderer renderer,
        IOptions<EmailOptions> emailOptions,
        TimeProvider timeProvider,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _recipients = recipients;
        _tenant = tenant;
        _settings = settings;
        _emailSender = emailSender;
        _renderer = renderer;
        _emailOptions = emailOptions.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<int> NotifyStaffAsync(
        StaffNotification notification, CancellationToken cancellationToken)
    {
        var agencyId = RequireTenant();

        var recipients = await _recipients.ForPermissionAsync(
            agencyId, notification.Permission, cancellationToken);

        if (recipients.Count == 0)
        {
            // Nobody entitled to see it. Not an error and not worth a warning:
            // an agency with no one holding Expense.Read gets no maintenance
            // alerts, which is the access rule working as intended.
            return 0;
        }

        var keys = recipients
            .Select(recipient => StaffDedupKey(recipient.UserId, notification))
            .ToList();

        // Tenant-filtered, so this only ever sees the agency's own rows.
        var alreadyTold = await _context.Notifications
            .AsNoTracking()
            .Where(n => keys.Contains(n.DedupKey))
            .Select(n => n.DedupKey)
            .ToListAsync(cancellationToken);

        var told = new HashSet<string>(alreadyTold, StringComparer.Ordinal);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var argsJson = notification.Args.ToJson();

        var raised = new List<(Notification Row, NotificationRecipient Recipient)>();

        foreach (var recipient in recipients)
        {
            var key = StaffDedupKey(recipient.UserId, notification);

            if (!told.Add(key))
            {
                continue;
            }

            var row = new Notification
            {
                AgencyId = agencyId,
                Kind = notification.Kind,
                MessageKey = notification.MessageKey,
                RecipientUserId = recipient.UserId,
                SubjectType = notification.SubjectType,
                SubjectId = notification.SubjectId,
                ClientId = notification.ClientId,
                ArgsJson = argsJson,
                Link = notification.Link,
                DedupKey = key,
                CreatedAt = now,
            };

            _context.Notifications.Add(row);
            raised.Add((row, recipient));
        }

        if (raised.Count == 0)
        {
            return 0;
        }

        if (!await TrySaveAsync(
                "raising staff notifications", raised.Select(r => r.Row), cancellationToken))
        {
            return 0;
        }

        var settings = await _settings.GetAsync(agencyId, cancellationToken);

        if (settings.NotifyStaffByEmail)
        {
            var agencyName = await AgencyNameAsync(agencyId, cancellationToken);

            foreach (var (row, recipient) in raised)
            {
                if (string.IsNullOrWhiteSpace(recipient.Email))
                {
                    continue;
                }

                var sent = await TrySendAsync(
                    recipient.Email,
                    notification.MessageKey,
                    notification.Args.Values,
                    recipient.Language,
                    recipient.FullName,
                    agencyName,
                    notification.Link,
                    cancellationToken);

                if (sent)
                {
                    row.RecipientEmail = recipient.Email;
                    row.EmailSentAt = _timeProvider.GetUtcNow().UtcDateTime;
                }
            }

            // The alerts themselves are already committed; this only records
            // which of them also went out by mail.
            await TrySaveAsync(
                "recording staff notification mail", raised.Select(r => r.Row), cancellationToken);
        }

        return raised.Count;
    }

    public async Task<ClientNotificationResult> NotifyClientAsync(
        ClientNotification notification, CancellationToken cancellationToken)
    {
        var agencyId = RequireTenant();

        if (string.IsNullOrWhiteSpace(notification.Email))
        {
            return new ClientNotificationResult(ClientNotificationOutcome.NoEmail);
        }

        var settings = await _settings.GetAsync(agencyId, cancellationToken);

        if (!settings.NotifyClientsByEmail && !notification.IgnoreClientEmailSetting)
        {
            return new ClientNotificationResult(ClientNotificationOutcome.Disabled);
        }

        var key = ClientDedupKey(notification);

        if (await _context.Notifications.AnyAsync(n => n.DedupKey == key, cancellationToken))
        {
            return new ClientNotificationResult(ClientNotificationOutcome.AlreadySent);
        }

        var row = new Notification
        {
            AgencyId = agencyId,
            Kind = notification.Kind,
            MessageKey = notification.MessageKey,
            // No RecipientUserId: this row is the record of a letter, not an
            // inbox entry, so it never shows up in anybody's notification list.
            RecipientEmail = notification.Email,
            ClientId = notification.ClientId,
            SubjectType = notification.SubjectType,
            SubjectId = notification.SubjectId,
            ArgsJson = notification.Args.ToJson(),
            DedupKey = key,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            SentByUserId = notification.SentByUserId,
        };

        _context.Notifications.Add(row);

        // Claim the dedup key before writing, so a concurrent sweep cannot mail
        // the same client twice: the unique index makes exactly one of the two
        // succeed. The cost is the rollback below, which is the trade worth
        // making — a duplicate reminder to a customer is worse than a retry.
        if (!await TrySaveAsync("reserving a client notification", new[] { row }, cancellationToken))
        {
            return new ClientNotificationResult(ClientNotificationOutcome.AlreadySent);
        }

        var agencyName = await AgencyNameAsync(agencyId, cancellationToken);

        var sent = await TrySendAsync(
            notification.Email!,
            notification.MessageKey,
            notification.Args.Values,
            notification.Language,
            null,
            agencyName,
            // Clients are not given a staff deep link; the reminder stands alone.
            link: null,
            cancellationToken);

        if (!sent)
        {
            // Undo the reservation rather than leave a row claiming the client
            // was written to. It also frees the dedup key, so the next sweep (or
            // the staff member pressing the button again) tries afresh — a row
            // kept "for the record" here would silently become a permanent
            // block on ever sending this reminder.
            _context.Notifications.Remove(row);
            await TrySaveAsync("releasing an unsent client notification", new[] { row }, cancellationToken);

            return new ClientNotificationResult(ClientNotificationOutcome.Failed);
        }

        row.EmailSentAt = _timeProvider.GetUtcNow().UtcDateTime;
        await TrySaveAsync("recording a client notification", new[] { row }, cancellationToken);

        return new ClientNotificationResult(ClientNotificationOutcome.Sent);
    }

    // ---------------------------------------------------------------------
    // Dedup keys. Format is internal and never parsed back — only compared —
    // but it is capped at the column's 200 characters, so the caller's token is
    // trimmed rather than allowed to overflow into a truncation error.
    // ---------------------------------------------------------------------

    private static string StaffDedupKey(string userId, StaffNotification notification) =>
        Key($"u:{userId}|k:{(int)notification.Kind}|m:{notification.MessageKey}"
            + $"|s:{(int)notification.SubjectType}:{notification.SubjectId}|t:{notification.DedupToken}");

    private static string ClientDedupKey(ClientNotification notification) =>
        Key($"c:{notification.ClientId}|k:{(int)notification.Kind}"
            + $"|s:{(int)notification.SubjectType}:{notification.SubjectId}|t:{notification.DedupToken}");

    private static string Key(string value) =>
        value.Length <= 200 ? value : value[..200];

    private int RequireTenant() =>
        _tenant.AgencyId
        ?? throw new InvalidOperationException(
            "Notifications are agency-scoped; push the tenant (AmbientTenant) before raising one.");

    private async Task<string> AgencyNameAsync(int agencyId, CancellationToken cancellationToken)
    {
        var name = await _context.Agencies
            .AsNoTracking()
            .Where(a => a.Id == agencyId)
            .Select(a => a.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(name) ? _emailOptions.FromName : name;
    }

    private async Task<bool> TrySendAsync(
        string email,
        string messageKey,
        IReadOnlyDictionary<string, string> args,
        string? language,
        string? recipientName,
        string agencyName,
        string? link,
        CancellationToken cancellationToken)
    {
        try
        {
            var culture = CultureOf(language);

            var text = _renderer.Render(
                messageKey, args, culture, recipientName, agencyName, AbsoluteLink(link));

            await _emailSender.SendEmailAsync(email, text.Subject, text.HtmlBody);

            return true;
        }
        catch (Exception exception)
        {
            // A mail server being down must not fail the sweep or the request
            // behind it: everything the message refers to already happened.
            _logger.LogError(
                exception,
                "Could not email the {MessageKey} notification to {Recipient}.", messageKey, email);

            return false;
        }
    }

    // A recipient who never chose a language gets the product default rather
    // than the server's locale, which is an accident of the host.
    private static CultureInfo CultureOf(string? language) =>
        CultureInfo.GetCultureInfo(Languages.Normalize(language) ?? Languages.Default);

    private string? AbsoluteLink(string? link)
    {
        if (string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(_emailOptions.PublicBaseUrl))
        {
            return null;
        }

        return $"{_emailOptions.PublicBaseUrl.TrimEnd('/')}/{link.TrimStart('/')}";
    }

    /// <summary>
    /// Saves, treating a rejected write as "already said" rather than an error:
    /// realistically it is the unique dedup index, and one contested alert must
    /// not abandon the rest of a sweep.
    /// </summary>
    /// <param name="owned">
    /// Every row this call put in the change tracker. On failure they are ALL
    /// detached, not just the one the database blamed — a sweep saves once per
    /// finding, on one context, across every agency in turn, so a row left behind
    /// as Added would be written by the NEXT agency's save and stamped with that
    /// agency's id by the tenant interceptor. Discarding the batch loses an alert
    /// the next run will find again; leaking one into another tenant is not
    /// recoverable.
    /// </param>
    private async Task<bool> TrySaveAsync(
        string what, IEnumerable<Notification> owned, CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception, "Discarded a duplicate notification while {What}.", what);

            foreach (var row in owned)
            {
                _context.Entry(row).State = EntityState.Detached;
            }

            // Anything else the failure implicated (an owned type, a fixed-up
            // reference) goes too, so the next save starts from a clean tracker.
            foreach (var entry in exception.Entries)
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }
}
