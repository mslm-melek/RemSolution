using RemSolution.Application.Common.Models;

namespace RemSolution.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<string?> GetUserNameAsync(string userId);

    Task<bool> IsInRoleAsync(string userId, string role);

    Task<bool> AuthorizeAsync(string userId, string policyName);

    Task<(Result Result, string UserId)> CreateUserAsync(string userName, string password);

    /// <summary>
    /// Creates a staff account inside an agency: the user is created with the
    /// AgencyId already set and the AgencyStaff role. Writes through the
    /// request-scoped DbContext, so it participates in an ambient transaction
    /// (the MaxUsers quota check relies on this to stay atomic).
    /// </summary>
    Task<(Result Result, string UserId)> CreateAgencyUserAsync(string userName, string password, int agencyId, CancellationToken cancellationToken);

    /// <summary>Users linked to the agency (admins included) — the MaxUsers quota base.</summary>
    Task<int> CountAgencyUsersAsync(int agencyId, CancellationToken cancellationToken);

    Task<Result> DeleteUserAsync(string userId);

    Task<Result> AssignAgencyAsync(string userId, int? agencyId);
}
