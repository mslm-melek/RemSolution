using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Models;
using RemSolution.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserClaimsPrincipalFactory<ApplicationUser> _userClaimsPrincipalFactory;
    private readonly IAuthorizationService _authorizationService;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        IUserClaimsPrincipalFactory<ApplicationUser> userClaimsPrincipalFactory,
        IAuthorizationService authorizationService)
    {
        _userManager = userManager;
        _userClaimsPrincipalFactory = userClaimsPrincipalFactory;
        _authorizationService = authorizationService;
    }

    public async Task<string?> GetUserNameAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        return user?.UserName;
    }

    public async Task<(Result Result, string UserId)> CreateUserAsync(string userName, string password)
    {
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = userName,
        };

        var result = await _userManager.CreateAsync(user, password);

        return (result.ToApplicationResult(), user.Id);
    }

    public async Task<(Result Result, string UserId)> CreateAgencyUserAsync(string userName, string password, int agencyId, string role, CancellationToken cancellationToken)
    {
        // AgencyId is set before the insert so the account is never observable
        // outside its agency, and the AgencyId claim is correct from the very
        // first sign-in.
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = userName,
            AgencyId = agencyId,
        };

        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            return (result.ToApplicationResult(), user.Id);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, role);

        return (roleResult.ToApplicationResult(), user.Id);
    }

    public async Task<int> CountAgencyUsersAsync(int agencyId, CancellationToken cancellationToken)
    {
        return await _userManager.Users.CountAsync(u => u.AgencyId == agencyId, cancellationToken);
    }

    public async Task<IReadOnlyList<AgencyUserRecord>> GetAgencyUsersAsync(int agencyId, CancellationToken cancellationToken)
    {
        var users = await _userManager.Users
            .Where(u => u.AgencyId == agencyId)
            .OrderBy(u => u.UserName)
            .ToListAsync(cancellationToken);

        var records = new List<AgencyUserRecord>(users.Count);

        foreach (var user in users)
        {
            records.Add(await ToRecordAsync(user));
        }

        return records;
    }

    public async Task<AgencyUserRecord?> GetAgencyUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);

        return user is null ? null : await ToRecordAsync(user);
    }

    public async Task<int?> GetUserAgencyIdAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);

        return user?.AgencyId;
    }

    private async Task<AgencyUserRecord> ToRecordAsync(ApplicationUser user)
    {
        // Agency users carry exactly one role by convention.
        var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty;
        var lockedOut = await _userManager.IsLockedOutAsync(user);

        return new AgencyUserRecord(user.Id, user.UserName ?? string.Empty, user.FullName, role, lockedOut);
    }

    public async Task<Result> UpdateUserRoleAndStampAsync(string userId, string? role, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return Result.Failure(new[] { $"User '{userId}' not found." });
        }

        if (role is not null)
        {
            var currentRoles = await _userManager.GetRolesAsync(user);

            if (!currentRoles.Contains(role))
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);

                if (!removeResult.Succeeded)
                {
                    return removeResult.ToApplicationResult();
                }

                var addResult = await _userManager.AddToRoleAsync(user, role);

                if (!addResult.Succeeded)
                {
                    return addResult.ToApplicationResult();
                }
            }
        }

        // Permission/role claims are minted at sign-in and re-minted on
        // security-stamp validation; bump the stamp so a changed grant applies
        // on the next request instead of lingering for the stamp interval.
        return (await _userManager.UpdateSecurityStampAsync(user)).ToApplicationResult();
    }

    public async Task<Result> SetUserLockoutAsync(string userId, bool lockedOut, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return Result.Failure(new[] { $"User '{userId}' not found." });
        }

        // Enable lockout on the account, then set (or clear) an indefinite end.
        await _userManager.SetLockoutEnabledAsync(user, true);

        var end = lockedOut ? DateTimeOffset.MaxValue : (DateTimeOffset?)null;
        var result = await _userManager.SetLockoutEndDateAsync(user, end);

        if (!result.Succeeded)
        {
            return result.ToApplicationResult();
        }

        // Invalidate existing sessions so a deactivation takes effect promptly.
        return (await _userManager.UpdateSecurityStampAsync(user)).ToApplicationResult();
    }

    public async Task<Result> AdminResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return Result.Failure(new[] { $"User '{userId}' not found." });
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

        return result.ToApplicationResult();
    }

    public async Task<MyProfileRecord?> GetProfileAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return null;
        }

        return new MyProfileRecord(user.UserName ?? string.Empty, user.FullName, user.Email, user.PreferredLanguage);
    }

    public async Task<Result> SetPreferredLanguageAsync(string userId, string language, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return Result.Failure(new[] { $"User '{userId}' not found." });
        }

        user.PreferredLanguage = language;

        return (await _userManager.UpdateAsync(user)).ToApplicationResult();
    }

    public async Task<string?> GetPreferredLanguageAsync(string userId, CancellationToken cancellationToken)
    {
        // A missing user is null, not an error: the caller is composing a message
        // and a language it cannot resolve simply falls back to the default.
        var user = await _userManager.FindByIdAsync(userId);

        return user?.PreferredLanguage;
    }

    public async Task<Result> SetHomeWidgetsAsync(
        string userId, IReadOnlyCollection<string> widgets, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return Result.Failure(new[] { $"User '{userId}' not found." });
        }

        // Serialize even an empty selection: the stored empty string is what
        // separates "chose nothing" from the null of "never chose".
        user.HomeWidgets = Domain.Constants.HomeWidgets.Serialize(widgets);

        return (await _userManager.UpdateAsync(user)).ToApplicationResult();
    }

    public async Task<Result> SetHomeActionsAsync(
        string userId, IReadOnlyCollection<string> actions, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return Result.Failure(new[] { $"User '{userId}' not found." });
        }

        // Serialize even an empty selection: the stored empty string is what
        // separates "chose nothing" from the null of "never chose".
        user.HomeActions = Domain.Constants.HomeActions.Serialize(actions);

        return (await _userManager.UpdateAsync(user)).ToApplicationResult();
    }

    public async Task<Result> UpdateProfileAsync(string userId, string? fullName, string email, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return Result.Failure(new[] { $"User '{userId}' not found." });
        }

        user.FullName = fullName;

        // The login is the email; keep username and email together so an email
        // change is a login change (Identity refreshes the security stamp, which
        // may require signing in again).
        if (!string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            var setEmail = await _userManager.SetEmailAsync(user, email);
            if (!setEmail.Succeeded)
            {
                return setEmail.ToApplicationResult();
            }

            var setUserName = await _userManager.SetUserNameAsync(user, email);
            if (!setUserName.Succeeded)
            {
                return setUserName.ToApplicationResult();
            }
        }

        return (await _userManager.UpdateAsync(user)).ToApplicationResult();
    }

    public async Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return Result.Failure(new[] { $"User '{userId}' not found." });
        }

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

        if (result.Succeeded)
        {
            // The password is now the user's own, so the account stops being
            // confined to this one action (see PasswordChangeRequiredMiddleware).
            await _userManager.ClearMustChangePasswordAsync(user);
        }

        return result.ToApplicationResult();
    }

    public async Task<bool> IsInRoleAsync(string userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId);

        return user != null && await _userManager.IsInRoleAsync(user, role);
    }

    public async Task<bool> AuthorizeAsync(string userId, string policyName)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return false;
        }

        var principal = await _userClaimsPrincipalFactory.CreateAsync(user);

        var result = await _authorizationService.AuthorizeAsync(principal, policyName);

        return result.Succeeded;
    }

    public async Task<Result> DeleteUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        return user != null ? await DeleteUserAsync(user) : Result.Success();
    }

    public async Task<Result> DeleteUserAsync(ApplicationUser user)
    {
        var result = await _userManager.DeleteAsync(user);

        return result.ToApplicationResult();
    }

    /// <summary>
    /// The only sanctioned way to change a user's agency. The AgencyId claim is
    /// minted at sign-in, so the security stamp must be refreshed to invalidate
    /// existing sessions — otherwise the user keeps acting as the old tenant
    /// until they happen to re-authenticate.
    /// </summary>
    public async Task<Result> AssignAgencyAsync(string userId, int? agencyId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Result.Failure(new[] { $"User '{userId}' not found." });
        }

        user.AgencyId = agencyId;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return result.ToApplicationResult();
        }

        return (await _userManager.UpdateSecurityStampAsync(user)).ToApplicationResult();
    }
}
