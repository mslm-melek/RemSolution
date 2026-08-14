namespace RemSolution.Application.Common.Models;

/// <summary>What happened when an agency was asked for its first administrator login.</summary>
public enum AgencyAdminOutcome
{
    /// <summary>Nothing was attempted.</summary>
    None = 0,

    /// <summary>Created; the temporary password has to reach a human.</summary>
    Created = 1,

    /// <summary>The agency already has a user, so nothing was created.</summary>
    AlreadyProvisioned = 2,

    /// <summary>
    /// The address is already an account on the platform. Nothing is created or
    /// adopted — re-purposing an identity would widen its privileges.
    /// </summary>
    EmailTaken = 3,

    /// <summary>No address to create the login with (the account IS its email).</summary>
    NoEmail = 4,
}

/// <summary>
/// The outcome of provisioning an agency's first administrator, with the
/// credentials when there are any. <see cref="TemporaryPassword"/> is plaintext
/// that lives for one request: never persisted, never logged. It IS returned to
/// the caller, who is the person onboarding the agency.
/// </summary>
public sealed record AgencyAdminAccountResult(
    AgencyAdminOutcome Outcome,
    string? UserId = null,
    string? Email = null,
    string? FullName = null,
    string? TemporaryPassword = null,
    string? AgencyName = null)
{
    public static readonly AgencyAdminAccountResult Nothing = new(AgencyAdminOutcome.None);

    /// <summary>True when there is a credentials email to send.</summary>
    public bool HasCredentials => !string.IsNullOrEmpty(TemporaryPassword);
}
