namespace RemSolution.Domain.Constants;

/// <summary>
/// What an agency user can pin to their home screen. Most are shortcut tiles —
/// a count plus a link into the list it counts; <see cref="Panels"/> are the
/// larger panels that render underneath them. Keys only: which feature and
/// permission a widget needs, and where it points, is presentation the SPA owns
/// (src/Web/ClientApp/src/app/shared/home-widgets.ts mirrors this list) — the
/// server's job here is to refuse a stored key it does not recognise.
///
/// A user's choice is persisted on their account, so these strings are a stored
/// contract: rename one and every user who pinned it loses the widget.
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
    // The month of pickups, returns and holds. A panel, not a tile — see Panels.
    public const string Calendar = nameof(Calendar);

    public static readonly string[] All =
    {
        Cars, Clients, Rentings, Reservations, Expenses, Credits, Chat,
        Brands, CarModels, ExpenseTypes, ExtraServiceTypes, DocumentTemplates,
        Calendar,
    };

    /// <summary>
    /// Widgets that render as a full-width panel under the tile row rather than as
    /// one count tile in it. They do not compete for the row's space, which is why
    /// <see cref="MaxPinned"/> does not count them.
    /// </summary>
    public static readonly string[] Panels = { Calendar };

    /// <summary>
    /// How many tiles one user may pin. The row wraps, so this is about the home
    /// screen staying a summary rather than becoming a second navigation bar.
    /// </summary>
    public const int MaxPinned = 8;

    public static bool IsKnown(string? key) =>
        key is not null && All.Contains(key);

    public static bool IsPanel(string? key) =>
        key is not null && Panels.Contains(key);

    /// <summary>How many of these keys count against <see cref="MaxPinned"/>.</summary>
    public static int CountTiles(IEnumerable<string> keys) =>
        keys.Count(k => !IsPanel(k));

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
