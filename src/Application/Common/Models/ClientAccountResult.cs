namespace RemSolution.Application.Common.Models;

/// <summary>
/// What happened when a client's email was turned into a customer-portal
/// account. Callers branch on this rather than on "did we get a password
/// back", because the difference between "linked an account the customer
/// already owns" and "created one for them" is exactly what the agency needs
/// to be told.
/// </summary>
public enum ClientAccountOutcome
{
    /// <summary>The client has no email address — nothing to provision.</summary>
    None = 0,

    /// <summary>
    /// A new Customer account was created;
    /// <see cref="ClientAccountResult.TemporaryPassword"/> is set and must be
    /// emailed.
    /// </summary>
    Created = 1,

    /// <summary>
    /// The address already belonged to a customer account (they registered on
    /// the marketplace, or another agency provisioned them), so the client was
    /// linked to it. No credentials are issued or sent: it is their account and
    /// their password.
    /// </summary>
    Linked = 2,

    /// <summary>The client was already linked to an account; nothing changed.</summary>
    AlreadyLinked = 3,

    /// <summary>
    /// A fresh temporary password was issued for an account that was
    /// provisioned but never used — the re-invite path, for when the first mail
    /// was lost. Only ever reached while the account still holds the password
    /// we generated for it.
    /// </summary>
    PasswordReset = 4,

    /// <summary>
    /// The address belongs to a staff or platform account. Nothing is linked:
    /// giving an operator's login a second life as a customer identity would
    /// mix the two sets of privileges, and quietly resetting a colleague's
    /// password from a client screen is worse still. The agency is told to use
    /// a different address.
    /// </summary>
    EmailBelongsToStaff = 5,

    /// <summary>
    /// The account exists and the customer has chosen their own password, so
    /// there is nothing to re-send — they sign in with what they picked, or use
    /// the forgotten-password flow. Distinct from
    /// <see cref="AlreadyLinked"/> only in that the caller asked to re-invite.
    /// </summary>
    AlreadyActive = 6,
}

/// <summary>
/// The outcome of provisioning, plus the credentials to mail when there are
/// any. <see cref="TemporaryPassword"/> is a plaintext secret that exists only
/// for the lifetime of the request that generated it: it is never persisted,
/// never logged, and never returned to the agency — only mailed to the address
/// it belongs to.
/// </summary>
public sealed record ClientAccountResult(
    ClientAccountOutcome Outcome,
    string? UserId = null,
    string? Email = null,
    string? FullName = null,
    string? TemporaryPassword = null)
{
    public static readonly ClientAccountResult Nothing = new(ClientAccountOutcome.None);

    /// <summary>True when there is a credentials email to send.</summary>
    public bool HasCredentials => !string.IsNullOrEmpty(TemporaryPassword);
}
