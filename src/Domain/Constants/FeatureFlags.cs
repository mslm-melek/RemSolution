namespace RemSolution.Domain.Constants;

/// <summary>
/// Per-agency feature toggles (AgencyFeature rows). A feature with no row is
/// ENABLED — rows exist to switch a module off (or explicitly back on), so
/// agencies work without any seeding. A disabled feature means 403 for every
/// request in the module — including the agency administrator — and the
/// module is hidden in the SPA.
/// </summary>
public abstract class FeatureFlags
{
    public const string Cars = nameof(Cars);
    public const string Clients = nameof(Clients);
    public const string Branches = nameof(Branches);

    /// <summary>Every known feature — drives the SPA's module list.</summary>
    public static readonly string[] All = { Cars, Clients, Branches };
}
