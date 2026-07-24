namespace RemSolution.Infrastructure.Data.Configurations;

public static class DatabaseCollations
{
    /// <summary>
    /// Accent- and case-insensitive collation for name/search columns, so
    /// French/Arabic client and entity names match regardless of accents or
    /// case (e.g. "Amelie" == "Amélie"). Chosen and applied up front (P.12):
    /// changing a column's collation later rebuilds every index that touches it.
    /// </summary>
    public const string AccentInsensitive = "Latin1_General_100_CI_AI";
}
