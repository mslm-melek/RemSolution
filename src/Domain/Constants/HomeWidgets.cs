namespace RemSolution.Domain.Constants;

/// <summary>
/// The shortcut tiles an agency user can pin to their home screen, each one a
/// count plus a link into the list it counts. Keys only: which feature and
/// permission a tile needs, and where it points, is presentation the SPA owns
/// (src/Web/ClientApp/src/app/shared/home-widgets.ts mirrors this list) — the
/// server's job here is to refuse a stored key it does not recognise.
///
/// A user's choice is persisted on their account, so these strings are a stored
/// contract: rename one and every user who pinned it loses the tile.
/// </summary>
public abstract class HomeWidgets
{
    public const string Cars = nameof(Cars);
    public const string Clients = nameof(Clients);
    public const string Rentings = nameof(Rentings);
    public const string Reservations = nameof(Reservations);
    public const string Expenses = nameof(Expenses);
    public const string Credits = nameof(Credits);
    public const string Chat = nameof(Chat);
    public const string Brands = nameof(Brands);
    public const string CarModels = nameof(CarModels);
    public const string ExpenseTypes = nameof(ExpenseTypes);
    public const string ExtraServiceTypes = nameof(ExtraServiceTypes);
    public const string DocumentTemplates = nameof(DocumentTemplates);

    public static readonly string[] All =
    {
        Cars, Clients, Rentings, Reservations, Expenses, Credits, Chat,
        Brands, CarModels, ExpenseTypes, ExtraServiceTypes, DocumentTemplates,
    };

    /// <summary>
    /// How many tiles one user may pin. The row wraps, so this is about the home
    /// screen staying a summary rather than becoming a second navigation bar.
    /// </summary>
    public const int MaxPinned = 8;

    public static bool IsKnown(string? key) =>
        key is not null && All.Contains(key);

    /// <summary>
    /// The stored form: an ordered, comma-separated list. Empty (not null) is a
    /// real choice — "show me no tiles" — so it round-trips as an empty list,
    /// while null means the user has never chosen and gets the default set.
    /// </summary>
    public static string Serialize(IEnumerable<string> keys) => string.Join(',', keys);

    // Unknown keys are dropped rather than failing the read: a key retired in a
    // later release must not cost a user the rest of their home screen. Distinct
    // for the same reason the writer refuses duplicates — one tile drawn twice is
    // a defect either way, and reads must not depend on how the row got there.
    public static IReadOnlyList<string>? Parse(string? stored) =>
        stored is null
            ? null
            : stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(IsKnown)
                .Distinct()
                .ToArray();
}
