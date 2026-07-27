using System.Globalization;
using Microsoft.AspNetCore.Localization;
using RemSolution.Domain.Constants;

namespace RemSolution.Web.Infrastructure;

/// <summary>
/// Request localization for the whole app: the SPA's API calls, the Identity
/// Razor pages and every validation message resolve their culture here.
/// </summary>
public static class RequestLocalizationSetup
{
    public static RequestLocalizationOptions Build()
    {
        var supported = Languages.All.Select(l => new CultureInfo(l)).ToList();

        var options = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(Languages.Default),
            SupportedCultures = supported,
            SupportedUICultures = supported
        };

        // Resolution order, most to least authoritative. The claim provider goes
        // first so a signed-in user's stored choice wins over a stale cookie left
        // by a different account on the same browser; the cookie then covers
        // anonymous pages (Login, Register) and the Accept-Language header covers
        // a first visit with no cookie yet.
        options.RequestCultureProviders.Insert(0, new PreferredLanguageClaimProvider());

        return options;
    }
}

/// <summary>
/// Reads the <see cref="Claims.PreferredLanguage"/> claim minted by
/// ApplicationUserClaimsPrincipalFactory. Anonymous requests fall through to the
/// cookie / Accept-Language providers.
/// </summary>
public sealed class PreferredLanguageClaimProvider : RequestCultureProvider
{
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var claim = httpContext.User?.FindFirst(Claims.PreferredLanguage)?.Value;
        var language = Languages.Normalize(claim);

        return Task.FromResult(language is null
            ? null
            : new ProviderCultureResult(language, language));
    }
}
