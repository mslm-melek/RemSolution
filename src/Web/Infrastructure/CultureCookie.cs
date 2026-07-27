using Microsoft.AspNetCore.Localization;

namespace RemSolution.Web.Infrastructure;

/// <summary>
/// Writes ASP.NET Core's culture cookie. Three places need it: the sign-in flow
/// (so the SPA boots in the account's stored language on a device that has never
/// seen it), the language endpoint used by the anonymous Identity pages, and the
/// authenticated language command. The SPA reads the same cookie before bootstrap
/// — see ClientApp/src/app/shared/language.ts.
/// </summary>
public static class CultureCookie
{
    public static void Write(HttpResponse response, string language)
    {
        response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(language)),
            new CookieOptions
            {
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                // A language choice is not tracking, so it survives a "reject
                // non-essential cookies" consent policy.
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            });
    }
}
