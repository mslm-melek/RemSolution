using RemSolution.Domain.Enums;

namespace RemSolution.Application.Common.Notifications;

/// <summary>
/// One thing to tell the agency's own staff. The service fans it out to everyone
/// entitled to see it — this describes the alert, not its recipients.
/// </summary>
/// <param name="Kind">Drives the icon, the severity colour and the list filter.</param>
/// <param name="MessageKey">The wording; see <see cref="NotificationMessages"/>.</param>
/// <param name="Permission">
/// What a staff member must hold to be told. This is the whole access rule for
/// notifications: an alert about a late hire goes to the people who can see
/// hires, so the inbox can never leak a booking to someone who could not open it
/// anyway. Agency administrators hold every permission, so they always qualify.
/// </param>
/// <param name="SubjectType">What the alert is about.</param>
/// <param name="SubjectId">Id of that record.</param>
/// <param name="Link">SPA route the alert opens.</param>
/// <param name="Args">Values interpolated into the wording.</param>
/// <param name="DedupToken">
/// What makes this alert distinct beyond its kind and subject — the time bucket,
/// and anything else that should let it repeat (a second expense type on the same
/// car). Two raises with the same token, kind, subject and recipient are one
/// notification.
/// </param>
/// <param name="ClientId">The client behind the alert, when there is one.</param>
public sealed record StaffNotification(
    NotificationKind Kind,
    string MessageKey,
    string Permission,
    NotificationSubject SubjectType,
    int? SubjectId,
    string? Link,
    NotificationArgs Args,
    string DedupToken,
    int? ClientId = null);

/// <summary>
/// One message to write to a client. Client notifications are mail only — the
/// stored row exists so the agency can see what went out and so a sweep cannot
/// send it twice (see <c>Notification</c>).
/// </summary>
/// <param name="Kind">Drives nothing visual here; it classifies the sent record.</param>
/// <param name="MessageKey">The wording; see <see cref="NotificationMessages"/>.</param>
/// <param name="ClientId">Who it is about — the row is filed against them.</param>
/// <param name="Email">Where to write. Nothing is sent (or recorded) without one.</param>
/// <param name="Language">
/// The client's language if known, so the mail arrives in it. Null falls back to
/// the agency's own working language.
/// </param>
/// <param name="SubjectType">The booking it concerns.</param>
/// <param name="SubjectId">Id of that booking.</param>
/// <param name="Args">Values interpolated into the wording.</param>
/// <param name="DedupToken">As on <see cref="StaffNotification"/>.</param>
/// <param name="SentByUserId">
/// Set when a person triggered this rather than the sweep — the manual late
/// notice. Null marks it as automatic.
/// </param>
/// <param name="IgnoreClientEmailSetting">
/// Sends even when the agency has automatic client mail switched off. Only the
/// manual notice sets this: a staff member pressing "inform the client" has made
/// the decision that switch exists to make.
/// </param>
public sealed record ClientNotification(
    NotificationKind Kind,
    string MessageKey,
    int ClientId,
    string? Email,
    string? Language,
    NotificationSubject SubjectType,
    int? SubjectId,
    NotificationArgs Args,
    string DedupToken,
    string? SentByUserId = null,
    bool IgnoreClientEmailSetting = false);

/// <summary>What became of a client message.</summary>
public enum ClientNotificationOutcome
{
    /// <summary>Mail left (or was handed to the logging sender in a dev checkout).</summary>
    Sent = 0,

    /// <summary>The client has no address on file, so there was nothing to write to.</summary>
    NoEmail = 1,

    /// <summary>Already sent for this booking in this window; not sent twice.</summary>
    AlreadySent = 2,

    /// <summary>The agency has client mail switched off.</summary>
    Disabled = 3,

    /// <summary>
    /// There was nothing to write about — asked for a late notice on a client
    /// whose cars are all back. Reported rather than thrown: the client list is a
    /// live screen, and a car can be returned between the page loading and the
    /// click.
    /// </summary>
    NothingToSend = 5,

    /// <summary>
    /// The row was recorded but the mail server refused it. The record is kept so
    /// the agency can see the attempt and try again.
    /// </summary>
    Failed = 4,
}

public sealed record ClientNotificationResult(ClientNotificationOutcome Outcome)
{
    public bool WasSent => Outcome == ClientNotificationOutcome.Sent;
}
