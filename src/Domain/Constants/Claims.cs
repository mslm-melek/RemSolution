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
}
