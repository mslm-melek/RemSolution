using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Notifications;

namespace RemSolution.Infrastructure.Notifications;

/// <summary>
/// Turns a stored notification (a message key plus its arguments) into the
/// subject and HTML body of an email, in a given language.
/// <para>
/// Substitutes the same <c>{{name}}</c> placeholders Transloco uses in the SPA
/// rather than the positional <c>{0}</c> that <c>string.Format</c> would: the two
/// renderers then read the same translation arguments by name, so a wording can
/// gain or reorder its values without a per-message argument list to keep in
/// step. That is worth the few lines of substitution below.
/// </para>
/// </summary>
public sealed class NotificationTextRenderer
{
    // {{name}}, with tolerance for the spacing people leave in translation files.
    private static readonly Regex Placeholder =
        new(@"\{\{\s*(?<name>[A-Za-z0-9_]+)\s*\}\}", RegexOptions.Compiled);

    private readonly ILocalizer _localizer;

    public NotificationTextRenderer(ILocalizer localizer)
    {
        _localizer = localizer;
    }

    /// <summary>
    /// Renders in <paramref name="culture"/>, whatever the ambient one is. The
    /// localizer resolves against <c>CurrentUICulture</c>, so the culture is
    /// swapped for the duration: the caller is a background sweep with no request
    /// culture, mailing several people who may each read a different language.
    /// </summary>
    public NotificationText Render(
        string messageKey,
        IReadOnlyDictionary<string, string> args,
        CultureInfo culture,
        string? recipientName,
        string? agencyName,
        string? absoluteLink)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = culture;

        try
        {
            var subject = Substitute(_localizer[$"Notification.{messageKey}.Subject"], args, culture);
            var message = Substitute(_localizer[$"Notification.{messageKey}.Body"], args, culture);

            return new NotificationText(subject, BuildHtml(message, recipientName, agencyName, absoluteLink));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>Replaces every <c>{{name}}</c> from <paramref name="args"/>.</summary>
    /// <remarks>
    /// An unknown or absent name collapses to nothing rather than being left as
    /// literal braces: a sentence with a gap in it still reads, a sentence with
    /// "{{clientName}}" in the middle looks broken to the customer receiving it.
    /// </remarks>
    private static string Substitute(
        string template, IReadOnlyDictionary<string, string> args, CultureInfo culture) =>
        Placeholder.Replace(template, match =>
        {
            var name = match.Groups["name"].Value;

            if (!args.TryGetValue(name, out var value) || string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return name.EndsWith(NotificationArgs.DateSuffix, StringComparison.Ordinal)
                ? FormatDate(value, culture)
                : value;
        });

    // Dates are stored ISO (see NotificationArgs) precisely so they can be
    // formatted for whoever is reading. Unparseable content is passed through
    // rather than dropped — better a raw date than a hole in the sentence.
    private static string FormatDate(string isoDate, CultureInfo culture) =>
        DateTime.TryParseExact(
            isoDate, NotificationArgs.IsoDateFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)
            ? parsed.ToString("d", culture)
            : isoDate;

    // Plain text in, HTML out. Every fragment is encoded — the argument values
    // are client and car names the agency typed, so they are not markup — and
    // blank-line-separated paragraphs in the resx become paragraphs here.
    private static string BuildHtml(
        string message, string? recipientName, string? agencyName, string? absoluteLink)
    {
        var encode = HtmlEncoder.Default;
        var html = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(recipientName))
        {
            html.Append("<p>").Append(encode.Encode(recipientName)).Append(",</p>");
        }

        foreach (var paragraph in message.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            html.Append("<p>").Append(encode.Encode(paragraph.Trim())).Append("</p>");
        }

        // Only when the deployment knows its own public address; see
        // EmailOptions.PublicBaseUrl for why it is never taken from a request.
        if (!string.IsNullOrWhiteSpace(absoluteLink))
        {
            html.Append("<p><a href=\"").Append(encode.Encode(absoluteLink)).Append("\">")
                .Append(encode.Encode(absoluteLink)).Append("</a></p>");
        }

        if (!string.IsNullOrWhiteSpace(agencyName))
        {
            html.Append("<p>").Append(encode.Encode(agencyName)).Append("</p>");
        }

        return html.ToString();
    }
}

public sealed record NotificationText(string Subject, string HtmlBody);
