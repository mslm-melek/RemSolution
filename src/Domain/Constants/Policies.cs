namespace RemSolution.Domain.Constants;

public abstract class Policies
{
    public const string PlatformAdminOnly = nameof(PlatformAdminOnly);
    public const string AgencyAdminOnly = nameof(AgencyAdminOnly);

    // Either administrator role. Used for managing global reference catalogs
    // (extra-service types, expense types) — the agency administrator curates
    // them for day-to-day use, the platform administrator as the app owner.
    public const string AgencyOrPlatformAdmin = nameof(AgencyOrPlatformAdmin);

    // A self-registered marketplace customer. Gates the customer-only booking
    // and my-reservations endpoints (browse is anonymous).
    public const string CustomerOnly = nameof(CustomerOnly);

    // Staff access is not a policy of its own anymore: each permission in
    // Permissions.All is registered as a policy of the same name (satisfied
    // by the permission claim, or implicitly by the AgencyAdministrator role).
}
