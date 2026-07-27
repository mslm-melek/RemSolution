namespace RemSolution.Domain.Constants;

/// <summary>
/// The UI languages the product ships. Single source of truth shared by the
/// request-localization setup, the profile command validator and the SPA
/// (src/Web/ClientApp/src/app/shared/language.ts mirrors this list).
/// </summary>
public abstract class Languages
{
    public const string English = "en";
    public const string French = "fr";

    /// <summary>
    /// Neutral Arabic. Deliberately neutral rather than regional: the SPA
    /// registers Maghrebi CLDR data under this tag so numbers render with
    /// Latin digits (350) rather than Arabic-Indic ones (٣٥٠), which is what
    /// prices and plate numbers are read as in this market.
    /// </summary>
    public const string Arabic = "ar";

    /// <summary>The product's default when nothing else resolves.</summary>
    public const string Default = French;

    public static readonly string[] All = [English, French, Arabic];

    public static bool IsSupported(string? language) =>
        language is not null && All.Contains(language);

    /// <summary>
    /// Whether the language reads right-to-left. Mirrors <c>isRtl()</c> in the
    /// SPA's language.ts; server-side renderers (generated PDFs) need the same
    /// answer the browser gets for <c>&lt;html dir&gt;</c>.
    /// </summary>
    public static bool IsRightToLeft(string? language) =>
        Normalize(language) == Arabic;

    /// <summary>
    /// Reduces a possibly-regional culture ("fr-TN") to a supported neutral
    /// language, or null when nothing matches.
    /// </summary>
    public static string? Normalize(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture)) return null;

        var neutral = culture.Split('-')[0].ToLowerInvariant();
        return IsSupported(neutral) ? neutral : null;
    }
}
