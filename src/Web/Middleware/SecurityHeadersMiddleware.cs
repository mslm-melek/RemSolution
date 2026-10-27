using Microsoft.Extensions.Options;

namespace RemSolution.Web.Middleware;

/// <summary>
/// Third-party origins the pages may load from beyond our own. The map's tile
/// server and the address lookup are deployment choices (environment.prod.ts
/// says to swap the OpenStreetMap defaults for a paid provider), so they are
/// configuration here too — change one and change the other, or the map goes
/// blank under the policy.
/// </summary>
public sealed class SecurityHeadersOptions
{
    public const string SectionName = "SecurityHeaders";

    /// <summary>Extra <c>img-src</c> origins: the map tiles.</summary>
    public string[] ImageSources { get; set; } = [];

    /// <summary>Extra <c>connect-src</c> origins: the geocoder the SPA calls directly.</summary>
    public string[] ConnectSources { get; set; } = [];
}

/// <summary>
/// The response headers every page should carry. The policy is strict for
/// scripts (our own files only, which is why the sign-in pages' scripts live in
/// <c>wwwroot/Identity/js</c> — under /Identity so the dev server proxies them —
/// and the Angular build does not inline critical CSS — that
/// feature works through an <c>onload</c> attribute) and lenient for styles,
/// which Angular and Material inject inline at run time.
///
/// <para>
/// The Content-Security-Policy goes on HTML only. A PDF opened from the uploads
/// is displayed by the browser's own viewer, which a document policy can stop
/// from loading. The Hangfire dashboard and the development Swagger UI are
/// third-party pages with their own inline scripts, so they are left out of it
/// too. Swagger UI only exists in Development, so only there is /api exempt.
/// </para>
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _contentSecurityPolicy;
    private readonly bool _swaggerUi;

    public SecurityHeadersMiddleware(
        RequestDelegate next, IOptions<SecurityHeadersOptions> options, IWebHostEnvironment environment)
    {
        _next = next;
        _swaggerUi = environment.IsDevelopment();

        var extraImages = string.Join(' ', options.Value.ImageSources);
        var extraConnect = string.Join(' ', options.Value.ConnectSources);

        _contentSecurityPolicy = string.Join("; ",
            "default-src 'self'",
            "script-src 'self'",
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
            "font-src 'self' https://fonts.gstatic.com data:",
            // data: and blob: for the previews built from a file before it is uploaded.
            $"img-src 'self' data: blob: {extraImages}".TrimEnd(),
            $"connect-src 'self' {extraConnect}".TrimEnd(),
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            "frame-ancestors 'none'");
    }

    public Task InvokeAsync(HttpContext context)
    {
        // Captured now: inside a Map branch the request's Path and PathBase are
        // rewritten, and restored only after the branch has written the response.
        var fullPath = context.Request.PathBase.Add(context.Request.Path);

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            // The origin still reaches the map and geocoder, whose usage policies
            // ask for it; the path — a client's page, a document's URL — does not.
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

            if (IsHtml(context) && !IsThirdPartyPage(fullPath))
            {
                headers.ContentSecurityPolicy = _contentSecurityPolicy;
            }

            return Task.CompletedTask;
        });

        return _next(context);
    }

    private static bool IsHtml(HttpContext context) =>
        context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true;

    private bool IsThirdPartyPage(PathString path) =>
        path.StartsWithSegments("/hangfire") || (_swaggerUi && path.StartsWithSegments("/api"));
}
