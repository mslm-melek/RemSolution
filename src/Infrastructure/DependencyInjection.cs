using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Constants;
using RemSolution.Infrastructure.Data;
using RemSolution.Infrastructure.Data.Interceptors;
using RemSolution.Infrastructure.Identity;
using RemSolution.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("RemSolutionDb");

        Guard.Against.Null(connectionString, message: "Connection string 'RemSolutionDb' not found.");

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

        builder.Services.AddTransient<IIdentityService, IdentityService>();
        builder.Services.AddScoped<ICrossTenantAccess, CrossTenantAccess>();

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
                    context.User.HasClaim(Claims.Permission, permission)));
            }
        });
    }
}
