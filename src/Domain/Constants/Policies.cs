namespace RemSolution.Domain.Constants;

public abstract class Policies
{
    public const string PlatformAdminOnly = nameof(PlatformAdminOnly);
    public const string AgencyAdminOnly = nameof(AgencyAdminOnly);

    // Staff access is not a policy of its own anymore: each permission in
    // Permissions.All is registered as a policy of the same name (satisfied
    // by the permission claim, or implicitly by the AgencyAdministrator role).
}
