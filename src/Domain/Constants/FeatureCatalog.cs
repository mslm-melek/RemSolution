namespace RemSolution.Domain.Constants;

/// <summary>
/// The single source of truth linking a <see cref="FeatureFlags">feature</see>
/// to the <see cref="Permissions">permissions</see> nested under it. A
/// permission is only meaningful while its feature is enabled for the agency,
/// so this map drives: which permissions the SPA offers per module, validation
/// that a staff grant belongs to an enabled feature, and the SPA menu tree.
///
/// Features not listed here (e.g. OnlineReservations, OnlinePayment) are
/// capabilities with no per-user permission — they gate a module on/off but
/// carry no staff actions.
/// </summary>
public static class FeatureCatalog
{
    public static readonly IReadOnlyDictionary<string, string[]> PermissionsByFeature =
        new Dictionary<string, string[]>
        {
            [FeatureFlags.Cars] = new[] { Permissions.CarCreate, Permissions.CarRead, Permissions.CarUpdate, Permissions.CarDelete },
            [FeatureFlags.Clients] = new[] { Permissions.ClientCreate, Permissions.ClientRead, Permissions.ClientUpdate, Permissions.ClientDelete },
            [FeatureFlags.Branches] = new[] { Permissions.BranchCreate, Permissions.BranchRead, Permissions.BranchUpdate, Permissions.BranchDelete },
            [FeatureFlags.Rentings] = new[] { Permissions.RentingCreate, Permissions.RentingRead, Permissions.RentingUpdate, Permissions.RentingDelete },
            [FeatureFlags.Reservations] = new[] { Permissions.ReservationCreate, Permissions.ReservationRead, Permissions.ReservationUpdate, Permissions.ReservationDelete },
            [FeatureFlags.Expenses] = new[] { Permissions.ExpenseCreate, Permissions.ExpenseRead, Permissions.ExpenseUpdate, Permissions.ExpenseDelete },
            [FeatureFlags.ExtraServices] = new[] { Permissions.ExtraServiceCreate, Permissions.ExtraServiceRead, Permissions.ExtraServiceUpdate, Permissions.ExtraServiceDelete },
            [FeatureFlags.Payments] = new[] { Permissions.PaymentCreate, Permissions.PaymentRead, Permissions.PaymentUpdate, Permissions.PaymentDelete },
            [FeatureFlags.Contracts] = new[] { Permissions.ContractGenerate },
            [FeatureFlags.Factures] = new[] { Permissions.FactureRead, Permissions.FactureGenerate },
            [FeatureFlags.Credits] = new[] { Permissions.CreditRead },
            [FeatureFlags.Dashboard] = new[] { Permissions.DashboardView },
            [FeatureFlags.Chat] = new[] { Permissions.ChatView },
            [FeatureFlags.OnlineReservations] = Array.Empty<string>(),
            [FeatureFlags.OnlinePayment] = Array.Empty<string>(),
        };

    private static readonly IReadOnlyDictionary<string, string> FeatureByPermissionMap =
        PermissionsByFeature
            .SelectMany(kvp => kvp.Value.Select(permission => (permission, feature: kvp.Key)))
            .ToDictionary(x => x.permission, x => x.feature);

    /// <summary>The feature a permission belongs to, or null if unknown.</summary>
    public static string? FeatureOf(string permission) =>
        FeatureByPermissionMap.TryGetValue(permission, out var feature) ? feature : null;

    /// <summary>The permissions belonging to a feature (empty if none).</summary>
    public static string[] PermissionsOf(string feature) =>
        PermissionsByFeature.TryGetValue(feature, out var permissions) ? permissions : Array.Empty<string>();

    /// <summary>Filters a set of permissions to those whose feature is enabled.</summary>
    public static IEnumerable<string> EffectivePermissions(IEnumerable<string> granted, ISet<string> enabledFeatures) =>
        granted.Where(p => FeatureByPermissionMap.TryGetValue(p, out var feature) && enabledFeatures.Contains(feature));
}
