using System.Security.Claims;
using System.Text;
using Hangfire;
using Hangfire.SqlServer;
using RemSolution.Application.Common.Documents;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Domain.Constants;
using RemSolution.Infrastructure.Booking;
using RemSolution.Infrastructure.Data;
using RemSolution.Infrastructure.Data.Interceptors;
using RemSolution.Infrastructure.Documents;
using RemSolution.Infrastructure.Email;
using RemSolution.Infrastructure.Identity;
using RemSolution.Infrastructure.Jobs;
using RemSolution.Infrastructure.Localization;
using RemSolution.Application.Common.Settings;
using RemSolution.Infrastructure.Imaging;
using RemSolution.Infrastructure.Pricing;
using RemSolution.Infrastructure.Settings;
using RemSolution.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
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

        // Development-only demo dataset; see DemoDataOptions for why it is opt-in.
        builder.Services.Configure<DemoDataOptions>(
            builder.Configuration.GetSection(DemoDataOptions.SectionName));
        builder.Services.AddScoped<DemoDataSeeder>();

        // .resx-backed localization for validation messages, API problem titles
        // and the Identity Razor pages. ResourcesPath is load-bearing: see the
        // SharedResource doc comment.
        builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
        builder.Services.AddSingleton<ILocalizer, ResourceLocalizer>();

        builder.Services
            .AddDefaultIdentity<ApplicationUser>(options =>
            {
                // The login IS the email everywhere in this app, and real email
                // addresses hold characters Identity's ASCII default rejects —
                // an accented or non-Latin local part would make "josé@…"
                // unregisterable, and worse, would make provisioning a client
                // account fail for a name the agency typed correctly. Empty
                // disables the character check; the address is still validated
                // as an email by the form/command that supplies it.
                options.User.AllowedUserNameCharacters = string.Empty;

                // Provisioning resolves an account BY email
                // (ClientAccountService), so two accounts sharing one address
                // would make "which user is this client?" ambiguous.
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>()
            // Identity's own failure text ("Email 'x' is already taken") is shown
            // verbatim on Register / ChangePassword, so it needs translating too.
            .AddErrorDescriber<LocalizedIdentityErrorDescriber>();

        // Outbound mail. Which sender is registered is a startup decision, not a
        // per-send branch: with no SMTP host configured the app takes the
        // logging fallback so a fresh checkout runs without a mail server (see
        // LoggingEmailSender). Registered AFTER AddDefaultIdentity, whose
        // Identity.UI default is a TryAdd no-op sender that would otherwise win.
        builder.Services.AddOptions<EmailOptions>()
            .Bind(builder.Configuration.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations();

        var emailOptions = builder.Configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>()
            ?? new EmailOptions();

        if (emailOptions.IsConfigured)
        {
            builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }

        // Turns a client's email into a customer-portal login. Scoped: it
        // creates the Identity user through the request's DbContext, so the
        // insert joins the caller's transaction (same reason as
        // CreateAgencyUserAsync).
        builder.Services.AddScoped<IClientAccountService, ClientAccountService>();

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

        // Car-availability overlap check shared by the renting/reservation flows
        // (queries the tenant-scoped booking sets, so it is DbContext-scoped).
        builder.Services.AddScoped<IAvailabilityChecker, AvailabilityChecker>();

        // Recurring reservation-expiry sweep (registered as a job below).
        builder.Services.AddScoped<ReservationExpiryJob>();

        // Car-image thumbnail/medium pipeline. The resizer is a stateless
        // singleton; the actual work runs as a Hangfire job (below).
        builder.Services.AddSingleton<IImageProcessor, SkiaImageProcessor>();
        builder.Services.AddScoped<CarImageProcessingJob>();

        // Generated rental paperwork (contracts, invoices). QuestPDF's licence
        // type is global process state, so it is set once here: Community is the
        // free tier and covers this product's revenue band — revisit if that
        // changes. Glyph checking is turned OFF deliberately: it throws when a
        // font lacks a glyph, which would turn an Arabic document on a host with
        // no Arabic font into a failed request instead of a rendered page (see
        // QuestPdfRentalDocumentRenderer.FontFor).
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;

        // Stateless apart from the localizer, but scoped so they resolve the
        // request's culture for document labels.
        builder.Services.AddScoped<IRentalDocumentRenderer, QuestPdfRentalDocumentRenderer>();
        builder.Services.AddScoped<IRentalDocumentService, RentalDocumentService>();
        builder.Services.AddScoped<IDocumentTemplateImporter, DocumentTemplateImporter>();
        // The platform's shipped example templates. A concrete class rather than an
        // interface: it has one implementation by definition (see the type).
        builder.Services.AddScoped<DocumentTemplateExamples>();

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
            options.AddPolicy(Policies.AgencyOrPlatformAdmin, policy =>
                policy.RequireRole(Roles.AgencyAdministrator, Roles.PlatformAdministrator));
            options.AddPolicy(Policies.CustomerOnly, policy => policy.RequireRole(Roles.Customer));

            // One policy per permission, named after it ("Client.Create", …),
            // usable both at endpoints and via [Authorize(Policy = ...)] on
            // requests. The agency administrator passes every permission
            // policy by role — no claims involved.
            foreach (var permission in Permissions.All)
            {
                options.AddPolicy(permission, policy => policy.RequireAssertion(context =>
                    context.User.IsInRole(Roles.AgencyAdministrator) ||
                    context.User.HasClaim(Claims.Permission, permission) ||
                    // A platform admin acting inside an agency's workspace
                    // through the impersonation middleware. The grant is every
                    // permission, not just the reads: the app owner has to be
                    // able to fix an agency's data, not only look at it. It is
                    // bounded elsewhere instead — the scope is only ever opened
                    // for the PlatformAdministrator role, only for one agency at
                    // a time, and every impersonated request writes an AuditLog
                    // row naming the acting user, the agency and the verb.
                    (context.User.IsInRole(Roles.PlatformAdministrator) &&
                     ImpersonationScope.IsActive)));
            }
        });
    }
}
