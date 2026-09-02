using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace RemSolution.Web.Infrastructure;

/// <summary>
/// Named rate-limit policies, applied per endpoint group. Everything else falls
/// through to the global limiter (see <see cref="RateLimitingExtensions"/>).
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// The anonymous marketplace. Its search is a spatial query with two
    /// anti-joins per candidate car and it is open to the world, so one script
    /// left running would otherwise saturate the database every agency shares.
    /// </summary>
    public const string PublicBrowse = nameof(PublicBrowse);

    /// <summary>
    /// Sign-in and token refresh. Identity's own lockout protects a single
    /// account; this protects the server against someone walking a password list
    /// across many accounts, which never trips a per-account lockout.
    /// </summary>
    public const string Authentication = nameof(Authentication);

    /// <summary>
    /// Self-service account creation. Unlimited, it mints accounts in bulk and
    /// sends verification mail from our domain — which lands the domain on a
    /// blocklist and takes every agency's legitimate mail down with it.
    /// </summary>
    public const string Registration = nameof(Registration);
}

/// <summary>
/// Limits, all per minute except registration. Tunable per environment because
/// the right number depends on how many staff sit behind one office NAT — the
/// defaults assume a couple of dozen.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Off only for a host that has no HTTP surface worth protecting (or to debug a throttle).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Catch-all for anonymous callers, keyed on client IP.</summary>
    public int AnonymousPerMinute { get; set; } = 120;

    /// <summary>Catch-all for signed-in callers, keyed on user id. Generous: a busy back-office screen is chatty.</summary>
    public int AuthenticatedPerMinute { get; set; } = 600;

    public int PublicBrowsePerMinute { get; set; } = 60;

    public int AuthenticationPerMinute { get; set; } = 10;

    public int RegistrationPerHour { get; set; } = 5;
}

public static class RateLimitingExtensions
{
    public static void AddRemSolutionRateLimiter(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<RateLimitingOptions>()
            .Bind(builder.Configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateOnStart();

        var limits = builder.Configuration
            .GetSection(RateLimitingOptions.SectionName)
            .Get<RateLimitingOptions>() ?? new RateLimitingOptions();

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // A signed-in caller is limited as a person, not as an address:
            // otherwise one office behind a single NAT throttles itself while a
            // botnet with a thousand addresses sails through.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

                return string.IsNullOrEmpty(userId)
                    ? FixedWindow($"anon:{ClientKey(context)}", limits.AnonymousPerMinute, TimeSpan.FromMinutes(1))
                    : FixedWindow($"user:{userId}", limits.AuthenticatedPerMinute, TimeSpan.FromMinutes(1));
            });

            AddIpPolicy(options, RateLimitPolicies.PublicBrowse, limits.PublicBrowsePerMinute, TimeSpan.FromMinutes(1));
            AddIpPolicy(options, RateLimitPolicies.Authentication, limits.AuthenticationPerMinute, TimeSpan.FromMinutes(1));
            AddIpPolicy(options, RateLimitPolicies.Registration, limits.RegistrationPerHour, TimeSpan.FromHours(1));

            options.OnRejected = async (context, cancellationToken) =>
            {
                // Only present on a fixed window, and it is the one thing a
                // well-behaved client actually needs from a 429.
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                // Same RFC-7807 shape CustomExceptionHandler produces, so the SPA
                // reads one error contract and not two.
                context.HttpContext.Response.ContentType = "application/problem+json";

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests",
                        Detail = "This client has made too many requests. Wait a moment and try again.",
                    },
                    cancellationToken);
            };
        });
    }

    /// <summary>
    /// Placed after authentication (so the global limiter can key on the user)
    /// and after the static-file and health-check middleware (so SPA assets and
    /// probes are never throttled). Absent entirely when disabled, which leaves
    /// the <c>RequireRateLimiting</c> metadata on the endpoints inert.
    /// </summary>
    public static void UseRemSolutionRateLimiter(this WebApplication app)
    {
        if (app.Services.GetRequiredService<IOptions<RateLimitingOptions>>().Value.Enabled)
        {
            app.UseRateLimiter();
        }
    }

    private static void AddIpPolicy(
        RateLimiterOptions options, string name, int permitLimit, TimeSpan window) =>
        options.AddPolicy(name, context =>
            FixedWindow($"{name}:{ClientKey(context)}", permitLimit, window));

    private static RateLimitPartition<string> FixedWindow(string key, int permitLimit, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            // No queue: a throttled caller should be told now, not held on a
            // thread until it gives up.
            QueueLimit = 0,
        });

    // Behind Azure App Service the socket peer is the front end, so the real
    // client only exists once UseForwardedHeaders has rewritten RemoteIpAddress
    // (see Program.cs). Without a remote address at all — an in-process test
    // host — everything shares one partition, which is the safe direction.
    private static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
