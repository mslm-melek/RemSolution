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
}
