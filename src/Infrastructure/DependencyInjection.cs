using System.Security.Claims;
using System.Text;
using Hangfire;
using Hangfire.SqlServer;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;
using RemSolution.Infrastructure.Data;
using RemSolution.Infrastructure.Data.Interceptors;
using RemSolution.Infrastructure.Identity;
using RemSolution.Application.Common.Settings;
using RemSolution.Infrastructure.Imaging;
using RemSolution.Infrastructure.Pricing;
using RemSolution.Infrastructure.Settings;
using RemSolution.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    // Default authentication scheme: a policy scheme that forwards to the JWT
    // bearer handler or the Identity cookie based on the request's headers.
    private const string MultiAuthScheme = "MultiAuth";

    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("RemSolutionDb");

        Guard.Against.Null(connectionString, message: "Connection string 'RemSolutionDb' not found.");

        // First: converts a delete of a soft-deletable entity into an archive,
        // so the stamping and audit interceptors below see the final state.
        builder.Services.AddScoped<ISaveChangesInterceptor, SoftDeleteInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, BaseEntityInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, TenantEntityInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, SubscriptionEnforcementInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();
        // Last: observes the values other interceptors have finalised before it
        // records the before/after audit rows.
        builder.Services.AddScoped<ISaveChangesInterceptor, AuditSaveChangesInterceptor>();

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(connectionString, x => x.UseNetTopologySuite())
            .AddAsyncSeeding(sp);
    });


        builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        builder.Services.AddScoped<ApplicationDbContextInitialiser>();

        builder.Services
            .AddDefaultIdentity<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>();

        // JWT bearer + refresh tokens for API/SPA clients. The access token
        // carries the same claims the cookie does (minted by the same claims
        // factory) and is short-lived; the refresh token is the long-lived,
        // revocable credential. See JwtOptions / TokenService.
        builder.Services.AddOptions<JwtOptions>()
            .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddScoped<ITokenService, TokenService>();

        // Read now (Key Vault is already merged in by this point) to configure
        // the bearer handler's validation parameters.
        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        // A policy scheme is the default: it forwards requests that present a
        // "Bearer" token to the JWT handler and everything else to the Identity
        // cookie, so the Angular SPA authenticates with tokens while the
        // Razor Identity pages keep working with cookies. AddDefaultIdentity set
        // the cookie as the default above; this re-points the default at the
        // selector.
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = MultiAuthScheme;
            options.DefaultChallengeScheme = MultiAuthScheme;
        })
        .AddJwtBearer(options =>
        {
            // Keep claim types verbatim so ClaimTypes.* (Name, Role,
            // NameIdentifier) and the custom AgencyId/Permission claims are read
            // back exactly as they were minted.
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                ValidateLifetime = true,
                // Keep the access-token lifetime honest; allow only minor drift.
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role
            };
        })
        .AddPolicyScheme(MultiAuthScheme, "JWT bearer or Identity cookie", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                string? authorization = context.Request.Headers.Authorization;

                return authorization?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
                    ? JwtBearerDefaults.AuthenticationScheme
                    : IdentityConstants.ApplicationScheme;
            };
        });

        // Session lifetime strategy. Permissions (and roles/AgencyId) live in
        // the auth ticket, so revocation must not wait for a re-login: the
        // ticket is treated as a short-lived access token. Every 10 minutes
        // the security-stamp validator re-validates it and REBUILDS the
        // principal through ApplicationUserClaimsPrincipalFactory — which
        // re-reads UserPermissions — so grants and revocations are live within
        // one interval, and a refreshed security stamp (user disabled, agency
        // reassigned) kills the session outright. No version-stamp machinery:
        // short validity + re-read on refresh is the whole mechanism.
        // Feature flags never ride in the ticket at all — they are read from
        // AgencyFeatures on every request by the FeatureEnforcementBehaviour.
        builder.Services.Configure<SecurityStampValidatorOptions>(options =>
        {
            options.ValidationInterval = TimeSpan.FromMinutes(10);
        });

        // The long-lived sliding cookie plays the refresh-token role: it only
        // proves who you are; what you may do is re-derived above.
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
        });

        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection(FileStorageOptions.SectionName));
        builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
        // Scoped: depends on the scoped IApplicationDbContext for dedup lookups.
        builder.Services.AddScoped<IStoredFileService, StoredFileService>();

        builder.Services.AddTransient<IIdentityService, IdentityService>();
        builder.Services.AddScoped<ICrossTenantAccess, CrossTenantAccess>();
        builder.Services.AddScoped<IImpersonationAuditor, ImpersonationAuditor>();

        // Per-agency settings, read through a cached provider (settings change
        // rarely; commands that change them invalidate the entry).
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<IAgencySettingsProvider, CachedAgencySettingsProvider>();

        // Stateless, side-effect-free pricing seam: the one place that turns a
        // car's DailyRate into a booking's snapshot price.
        builder.Services.AddSingleton<IPricingService, PricingService>();

        // Car-image thumbnail/medium pipeline. The resizer is a stateless
        // singleton; the actual work runs as a Hangfire job (below).
        builder.Services.AddSingleton<IImageProcessor, SkiaImageProcessor>();
        builder.Services.AddScoped<CarImageProcessingJob>();

        // Hangfire is the single background-job infrastructure (P.10). Skip it
        // when there is no real database to talk to: the NSwag build-time host
        // uses a placeholder connection string (SqlServerStorage would connect
        // eagerly), and functional tests turn it off (Hangfire:Enabled=false) so
        // no job server races the per-test database reset. In those cases the
        // enqueue seam becomes a no-op.
        var hangfireEnabled = builder.Configuration.GetValue("Hangfire:Enabled", true)
            && connectionString != "NSwagBuildTimePlaceholder";

        if (hangfireEnabled)
        {
            builder.Services.AddHangfire(configuration => configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
                {
                    // Hangfire manages its own [HangFire] schema, independent of
                    // the EF migrations.
                    SchemaName = "HangFire",
                    PrepareSchemaIfNecessary = true,
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.FromSeconds(15),
                    UseRecommendedIsolationLevel = true,
                    DisableGlobalLocks = true
                }));

            builder.Services.AddHangfireServer();

            builder.Services.AddScoped<IImageProcessingQueue, HangfireImageProcessingQueue>();
        }
        else
        {
            builder.Services.AddSingleton<IImageProcessingQueue, NoOpImageProcessingQueue>();
        }

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.PlatformAdminOnly, policy => policy.RequireRole(Roles.PlatformAdministrator));
            options.AddPolicy(Policies.AgencyAdminOnly, policy => policy.RequireRole(Roles.AgencyAdministrator));

            // One policy per permission, named after it ("Client.Create", …),
            // usable both at endpoints and via [Authorize(Policy = ...)] on
            // requests. The agency administrator passes every permission
            // policy by role — no claims involved.
            foreach (var permission in Permissions.All)
            {
                options.AddPolicy(permission, policy => policy.RequireAssertion(context =>
                    context.User.IsInRole(Roles.AgencyAdministrator) ||
                    context.User.HasClaim(Claims.Permission, permission) ||
                    // A platform admin browsing a tenant read-only through the
                    // impersonation middleware: only while the ambient scope is
                    // active, and only for the read permissions.
                    (context.User.IsInRole(Roles.PlatformAdministrator) &&
                     ImpersonationScope.IsActive &&
                     Permissions.ReadOnly.Contains(permission))));
            }
        });
    }
}
