using RemSolution.Application.Common.Notifications;

namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// Records notifications and delivers them. The one write path into the
/// <c>Notifications</c> table: it owns the deduplication, the fan-out to
/// entitled staff, and the mail.
/// <para>
/// Callers describe an alert, never its recipients or its wording. Both the
/// scheduled sweep and the hand-sent client notice go through here, which is what
/// keeps "who may be told this" and "did we already say it" in one place instead
/// of once per event type.
/// </para>
/// <para>
/// Implemented in Infrastructure because resolving recipients needs the Identity
/// store and delivery needs the mail sender — the same split as
/// <see cref="IClientAccountService"/>.
/// </para>
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Raises an alert for every staff member of the current tenant who holds the
    /// request's permission, and mails it to them if the agency has staff mail on.
    /// Returns how many notifications were newly created — zero means everyone
    /// entitled had already been told (see the dedup token), which is the normal
    /// result of the sweep re-running.
    /// <para>
    /// Saves its own changes: it is called in a loop over findings, and one
    /// unwritable row must not discard the rest of the sweep.
    /// </para>
    /// </summary>
    Task<int> NotifyStaffAsync(StaffNotification notification, CancellationToken cancellationToken);

    /// <summary>
    /// Writes to a client once, recording the attempt. Mail failure is reported
    /// through the result rather than thrown: nothing this mail refers to is
    /// undone by a mail server being down, and the caller is either a background
    /// sweep with more work to do or a screen that should say "not sent" rather
    /// than "error".
    /// </summary>
    Task<ClientNotificationResult> NotifyClientAsync(
        ClientNotification notification, CancellationToken cancellationToken);
}
