namespace RemSolution.Domain.Constants;

public abstract class Claims
{
    public const string AgencyId = nameof(AgencyId);

    /// <summary>
    /// One claim per permission the user holds (see <see cref="Permissions"/>),
    /// minted from UserPermission rows at sign-in and re-minted at every
    /// security-stamp validation (10-minute interval — the session ticket is a
    /// short-lived access token). Granting or revoking is therefore live
    /// within one interval; refresh the security stamp to force it at the
    /// next request.
    /// </summary>
    public const string Permission = nameof(Permission);

    /// <summary>
    /// The user's stored UI language (see <see cref="Languages"/>), minted from
    /// ApplicationUser.PreferredLanguage. Read by the request-localization
    /// provider so it takes precedence over the culture cookie and the browser's
    /// Accept-Language, which makes the choice follow the account across devices.
    /// Absent when the user has never picked a language.
    /// </summary>
    public const string PreferredLanguage = nameof(PreferredLanguage);

    /// <summary>
    /// Present (value "true") while the account still holds a password somebody
    /// else chose — see ApplicationUser.MustChangePassword. Minted from the user
    /// row like every other claim here, so clearing the flag needs the security
    /// stamp refreshed to take effect before the 10-minute validation interval;
    /// the change-password paths do exactly that. Read by
    /// PasswordChangeRequiredMiddleware, which lets only the change-password
    /// endpoints through, and surfaced to the SPA on /users/me.
    /// </summary>
    public const string MustChangePassword = nameof(MustChangePassword);
}
