namespace RemSolution.Domain.Common;

/// <summary>
/// The name of a catalog entry (an expense or add-on type). A platform template's
/// name is a translation key — <c>oilChange</c>, not « Vidange » — and each reader
/// translates it: Transloco's <c>catalogItems.*</c> in the SPA, the resx
/// <c>CatalogItem.*</c> for the invoice and the mail. A name nobody translated (an
/// agency's own type, or a copy it renamed) is shown exactly as typed.
/// </summary>
public static class CatalogName
{
    public const int MaxLength = 200;
}
