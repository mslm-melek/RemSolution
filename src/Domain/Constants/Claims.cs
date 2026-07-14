namespace RemSolution.Domain.Constants;

public abstract class Claims
{
    public const string AgencyId = nameof(AgencyId);

    /// <summary>
    /// One claim per permission the user holds (see <see cref="Permissions"/>),
    /// minted at sign-in from UserPermission rows. Granting or revoking takes
    /// effect at the next sign-in (refresh the security stamp to force it).
    /// </summary>
    public const string Permission = nameof(Permission);
}
