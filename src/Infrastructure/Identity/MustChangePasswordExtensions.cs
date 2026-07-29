using Microsoft.AspNetCore.Identity;

namespace RemSolution.Infrastructure.Identity;

/// <summary>
/// One place to retire the temporary-password flag, shared by the two ways a
/// user can change their own password — the API command and the Razor manage
/// page. Kept together so neither can be updated without the other and leave a
/// customer who HAS chosen a password still locked out by the middleware.
/// </summary>
public static class MustChangePasswordExtensions
{
    /// <summary>
    /// Clears <see cref="ApplicationUser.MustChangePassword"/> if it is set.
    /// A no-op for the ordinary case, so callers can invoke it after every
    /// successful password change without checking first.
    /// </summary>
    public static async Task ClearMustChangePasswordAsync(
        this UserManager<ApplicationUser> userManager, ApplicationUser user)
    {
        if (!user.MustChangePassword)
        {
            return;
        }

        user.MustChangePassword = false;

        // UpdateAsync, not just a context save: it goes through the same store
        // the claims factory reads, and Identity has already rotated the
        // security stamp as part of the password change, so the next ticket is
        // minted without the claim.
        await userManager.UpdateAsync(user);
    }
}
