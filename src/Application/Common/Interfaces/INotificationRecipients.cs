namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// A member of staff who is to be told something.
/// </summary>
/// <param name="UserId">Identity user id — the notification's addressee.</param>
/// <param name="Email">Where to mail them; null/blank means in-app only.</param>
/// <param name="FullName">For the greeting in the mail.</param>
/// <param name="Language">
/// Their chosen UI language, or null if they never chose one. The mail is
/// composed in it, so two colleagues reading the same alert each get their own.
/// </param>
public sealed record NotificationRecipient(
    string UserId,
    string? Email,
    string? FullName,
    string? Language);

/// <summary>
/// Answers "who in this agency should hear about this?". Lives behind an
/// interface because the answer is in the Identity store (users, roles, lockout)
/// which the Application layer does not reference.
/// </summary>
public interface INotificationRecipients
{
    /// <summary>
    /// The agency's active staff who hold <paramref name="permission"/> —
    /// administrators included, since they hold every permission implicitly, the
    /// same rule the authorization policies apply.
    /// <para>
    /// Deactivated (locked-out) accounts are excluded: an alert addressed to
    /// somebody who cannot sign in is an unread count nobody can clear.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<NotificationRecipient>> ForPermissionAsync(
        int agencyId, string permission, CancellationToken cancellationToken);
}
