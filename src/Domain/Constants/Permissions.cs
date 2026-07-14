namespace RemSolution.Domain.Constants;

/// <summary>
/// Fine-grained permission names, granted per user (UserPermission rows,
/// loaded into the principal as <see cref="Claims.Permission"/> claims at
/// sign-in) and enforced through an authorization policy registered per
/// permission. This replaces all-or-nothing staff access: what a staff
/// member can do is the set of permissions they hold. AgencyAdministrator
/// holds every permission implicitly (checked by role in the policy, never
/// materialized as claims).
/// </summary>
public abstract class Permissions
{
    public const string CarCreate = "Car.Create";
    public const string CarRead = "Car.Read";
    public const string CarUpdate = "Car.Update";
    public const string CarDelete = "Car.Delete";

    public const string ClientCreate = "Client.Create";
    public const string ClientRead = "Client.Read";
    public const string ClientUpdate = "Client.Update";
    public const string ClientDelete = "Client.Delete";

    /// <summary>Every known permission — drives policy registration.</summary>
    public static readonly string[] All =
    {
        CarCreate, CarRead, CarUpdate, CarDelete,
        ClientCreate, ClientRead, ClientUpdate, ClientDelete,
    };
}
