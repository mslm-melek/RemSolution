using RemSolution.Application.Common.Models;

namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// Gives a newly created agency somebody who can sign in to it.
/// <para>
/// Two calls because they sit on opposite sides of a commit, like
/// <see cref="IClientAccountService"/>: <see cref="EnsureAdministratorAsync"/>
/// writes through the request's DbContext (inside the transaction), while
/// <see cref="SendWelcomeAsync"/> talks to SMTP and must run after the commit.
/// </para>
/// </summary>
public interface IAgencyAccountService
{
    /// <summary>
    /// Creates the agency's first <c>AgencyAdministrator</c> login with a temporary
    /// password. Idempotent: an agency that already has any user is left alone.
    /// </summary>
    Task<AgencyAdminAccountResult> EnsureAdministratorAsync(
        int agencyId, string? agencyName, string? email, string? fullName, CancellationToken cancellationToken);

    /// <summary>
    /// Emails the credentials, if there are any, and says whether a message went
    /// out. Never throws — the caller is handed the same credentials on screen.
    /// </summary>
    Task<bool> SendWelcomeAsync(AgencyAdminAccountResult account, CancellationToken cancellationToken);
}
