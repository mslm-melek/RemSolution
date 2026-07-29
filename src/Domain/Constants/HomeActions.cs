namespace RemSolution.Domain.Constants;

/// <summary>
/// The quick actions a user can keep on their landing screen — the "start
/// something" shortcuts that sit under the figures. Keys only, exactly like
/// <see cref="HomeWidgets"/>: where each action points, which icon it carries and
/// which role or feature may be offered it is presentation the SPA owns
/// (src/Web/ClientApp/src/app/shared/home-actions.ts mirrors this list).
///
/// A user's choice is persisted on their account, so these strings are a stored
/// contract: rename one and every user who picked it loses the action.
/// </summary>
public abstract class HomeActions
{
    // --- Platform-admin console -----------------------------------------------
    public const string NewAgency = nameof(NewAgency);
    public const string NewPlan = nameof(NewPlan);
    public const string NewCarModel = nameof(NewCarModel);
    public const string CarBrands = nameof(CarBrands);
    public const string CarModels = nameof(CarModels);
    public const string ExpenseTypes = nameof(ExpenseTypes);
    public const string ExtraServiceTypes = nameof(ExtraServiceTypes);
    public const string Agencies = nameof(Agencies);
    public const string SubscriptionPlans = nameof(SubscriptionPlans);
    public const string BrowseMarketplace = nameof(BrowseMarketplace);

    // --- Agency workspace -----------------------------------------------------
    public const string NewCar = nameof(NewCar);
    public const string NewRenting = nameof(NewRenting);
    public const string NewReservation = nameof(NewReservation);
    public const string NewClient = nameof(NewClient);
    public const string NewExpense = nameof(NewExpense);

    public static readonly string[] All =
    {
        NewAgency, NewPlan, NewCarModel, CarBrands, CarModels, ExpenseTypes,
        ExtraServiceTypes, Agencies, SubscriptionPlans, BrowseMarketplace,
        NewCar, NewRenting, NewReservation, NewClient, NewExpense,
    };

    /// <summary>
    /// How many actions one user may keep. The row wraps, so this is about the
    /// action strip staying a shortlist rather than becoming a second menu.
    /// </summary>
    public const int MaxPinned = 6;

    public static bool IsKnown(string? key) =>
        key is not null && All.Contains(key);

    /// <summary>
    /// The stored form: an ordered, comma-separated list. Empty (not null) is a
    /// real choice — "show me no actions" — so it round-trips as an empty list,
    /// while null means the user has never chosen and gets the default set.
    /// </summary>
    public static string Serialize(IEnumerable<string> keys) => string.Join(',', keys);

    // Unknown keys are dropped rather than failing the read: a key retired in a
    // later release must not cost a user the rest of their action strip.
    public static IReadOnlyList<string>? Parse(string? stored) =>
        stored is null
            ? null
            : stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(IsKnown)
                .Distinct()
                .ToArray();
}
