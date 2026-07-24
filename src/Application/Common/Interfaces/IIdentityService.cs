using RemSolution.Application.Common.Models;

namespace RemSolution.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<string?> GetUserNameAsync(string userId);

    Task<bool> IsInRoleAsync(string userId, string role);

    Task<bool> AuthorizeAsync(string userId, string policyName);

    Task<(Result Result, string UserId)> CreateUserAsync(string userName, string password);

    /// <summary>
    /// Creates a user inside an agency: created with the AgencyId already set and
    /// the given role (<see cref="Domain.Constants.Roles.AgencyAdministrator"/> or
    /// <see cref="Domain.Constants.Roles.AgencyStaff"/>). Writes through the
    /// request-scoped DbContext, so it participates in an ambient transaction
    /// (the MaxUsers quota check relies on this to stay atomic).
    /// </summary>
    Task<(Result Result, string UserId)> CreateAgencyUserAsync(string userName, string password, int agencyId, string role, CancellationToken cancellationToken);

    /// <summary>Users linked to the agency (admins included) — the MaxUsers quota base.</summary>
    Task<int> CountAgencyUsersAsync(int agencyId, CancellationToken cancellationToken);

    /// <summary>Identity projection of every user in an agency (role + lockout state).</summary>
    Task<IReadOnlyList<AgencyUserRecord>> GetAgencyUsersAsync(int agencyId, CancellationToken cancellationToken);

    /// <summary>Identity projection of one user, or null if not found.</summary>
    Task<AgencyUserRecord?> GetAgencyUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>The agency a user belongs to (null if none / user not found) — used for tenant ownership checks.</summary>
    Task<int?> GetUserAgencyIdAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the user's role (when <paramref name="role"/> is non-null) and
    /// always refreshes the security stamp so the permission/role claims re-mint
    /// on the next request rather than lingering for the stamp interval.
    /// </summary>
    Task<Result> UpdateUserRoleAndStampAsync(string userId, string? role, CancellationToken cancellationToken);

    /// <summary>Deactivates (locks out indefinitely) or reactivates a user.</summary>
    Task<Result> SetUserLockoutAsync(string userId, bool lockedOut, CancellationToken cancellationToken);

    /// <summary>Administratively sets a new password (bypasses the current-password check).</summary>
    Task<Result> AdminResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken);

    Task<Result> DeleteUserAsync(string userId);

    Task<Result> AssignAgencyAsync(string userId, int? agencyId);
}
