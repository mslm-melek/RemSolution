using System.Text.Json;

namespace RemSolution.Application.Common.Notifications;

/// <summary>
/// The values interpolated into a notification's message, and the one place that
/// knows how they are stored.
/// <para>
/// Both renderers substitute the same <c>{{name}}</c> placeholders — Transloco
/// does it natively in the SPA, and <c>NotificationTextRenderer</c> does it over
/// the resx strings on the server — so a wording needs no per-message argument
/// ordering and the two sides cannot drift apart.
/// </para>
/// <para>
/// Every value is a string. Numbers are formatted by whoever writes them (they
/// are counts, not quantities to compute with); dates are the exception and
/// carry the convention below.
/// </para>
/// </summary>
public sealed class NotificationArgs
{
    /// <summary>
    /// An argument whose name ends with this holds a round-trippable ISO date
    /// (<c>yyyy-MM-dd</c>) rather than a formatted one, because the reader's
    /// culture is not known when the notification is raised — the same row is
    /// read by a staff member in French and mailed to a client in Arabic. Each
    /// renderer formats it at the moment it renders.
    /// </summary>
    public const string DateSuffix = "Date";

    public const string IsoDateFormat = "yyyy-MM-dd";

    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public NotificationArgs Set(string name, string? value)
    {
        // A missing value would render as a literal "{{car}}" in the middle of a
        // sentence, so an absent one is stored as an empty string and the wording
        // is written to survive it.
        _values[name] = value ?? string.Empty;
        return this;
    }

    public NotificationArgs Set(string name, int value) =>
        Set(name, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// Stores a date under a name ending in <see cref="DateSuffix"/>, in the ISO
    /// form both renderers expect. Guards the naming rather than trusting it: a
    /// date filed under a name the renderers do not recognise would reach the
    /// reader as "2026-08-14" in the middle of a French sentence.
    /// </summary>
    public NotificationArgs SetDate(string name, DateTime? value)
    {
        if (!name.EndsWith(DateSuffix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Date argument '{name}' must be named with the '{DateSuffix}' suffix so renderers format it.",
                nameof(name));
        }

        return Set(name, value?.ToString(IsoDateFormat, System.Globalization.CultureInfo.InvariantCulture));
    }

    public IReadOnlyDictionary<string, string> Values => _values;

    public string ToJson() => JsonSerializer.Serialize(_values);

    /// <summary>
    /// Reads a stored argument set back. Never throws on bad content: a
    /// notification with unreadable arguments should still render its wording
    /// (with empty placeholders) rather than take a screen down.
    /// </summary>
    public static IReadOnlyDictionary<string, string> FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }
}
