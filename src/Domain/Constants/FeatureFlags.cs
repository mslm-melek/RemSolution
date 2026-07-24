namespace RemSolution.Domain.Constants;

/// <summary>
/// Per-agency capability modules. A feature is enabled for an agency when its
/// active subscription plan includes it, unless an <c>AgencyFeature</c> override
/// row forces it on or off (see the feature resolver). No active subscription
/// means no features. A disabled feature = 403 for every request in the module
/// (agency administrator included) and the module is hidden in the SPA.
///
/// See <see cref="FeatureCatalog"/> for the feature → permission mapping.
/// </summary>
public abstract class FeatureFlags
{
    public const string Cars = nameof(Cars);
    public const string Clients = nameof(Clients);
    public const string Branches = nameof(Branches);
    public const string Rentings = nameof(Rentings);
    public const string Reservations = nameof(Reservations);
    public const string Expenses = nameof(Expenses);
    public const string ExtraServices = nameof(ExtraServices);
    public const string Payments = nameof(Payments);
    public const string Contracts = nameof(Contracts);
    public const string Factures = nameof(Factures);
    public const string Credits = nameof(Credits);
    public const string Dashboard = nameof(Dashboard);
    public const string Chat = nameof(Chat);
    public const string OnlineReservations = nameof(OnlineReservations);
    public const string OnlinePayment = nameof(OnlinePayment);

    /// <summary>Every known feature — drives plan setup, the resolver and the SPA menu.</summary>
    public static readonly string[] All =
    {
        Cars, Clients, Branches, Rentings, Reservations, Expenses, ExtraServices,
        Payments, Contracts, Factures, Credits, Dashboard, Chat,
        OnlineReservations, OnlinePayment,
    };
}
