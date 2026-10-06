namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// Server-side reading of a catalog name (<see cref="Domain.Common.CatalogName"/>),
/// for what is rendered without the SPA — the invoice and the mail. The resx keys
/// mirror Transloco's <c>catalogItems.*</c>; keep the two lists together.
/// </summary>
public static class CatalogNameLocalization
{
    public const string ResourcePrefix = "CatalogItem.";

    /// <summary>
    /// The translation of <paramref name="name"/> in the current UI culture, or
    /// <paramref name="name"/> itself when it is not a key (an agency's own words).
    /// </summary>
    public static string? CatalogName(this ILocalizer localizer, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var key = ResourcePrefix + name;
        var translated = localizer[key];

        // ILocalizer answers a missing key with the key itself.
        return translated == key ? name : translated;
    }
}
