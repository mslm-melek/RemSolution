using Azure.Identity;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Infrastructure;
using RemSolution.Infrastructure.Data;
using RemSolution.Web.Infrastructure;
using RemSolution.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;


namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddWebServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddScoped<IUser, CurrentUser>();
        builder.Services.AddScoped<ITenantProvider, CurrentTenant>();

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>();

        builder.Services.AddExceptionHandler<CustomExceptionHandler>();

        builder.AddRemSolutionRateLimiter();

        // The Identity pages are server-rendered, so their text and their
        // DataAnnotations messages resolve through the same shared .resx set the
        // API and the SPA use.
        builder.Services.AddRazorPages(options =>
            {
                // The three anonymous pages that cost us something to serve: two
                // send mail from our own domain and one creates accounts. Razor
                // Pages has no per-page endpoint builder, so the policy goes on
                // as page metadata instead (see RateLimitPolicies.Registration).
                foreach (var page in new[] { "/Account/Register", "/Account/ForgotPassword", "/Account/ResetPassword" })
                {
                    options.Conventions.AddAreaPageApplicationModelConvention("Identity", page, model =>
                        model.EndpointMetadata.Add(
                            new EnableRateLimitingAttribute(RateLimitPolicies.Registration)));
                }
            })
            .AddViewLocalization()
            .AddDataAnnotationsLocalization(options =>
                options.DataAnnotationLocalizerProvider = (_, factory) =>
                    factory.Create(typeof(SharedResource)));

        // Customise default API behaviour
        builder.Services.Configure<ApiBehaviorOptions>(options =>
            options.SuppressModelStateInvalidFilter = true);

        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddOpenApiDocument((configure, sp) =>
        {
            configure.Title = "RemSolution API";

        });
    }

    public static void AddKeyVaultIfConfigured(this IHostApplicationBuilder builder)
    {
        var keyVaultUri = builder.Configuration["AZURE_KEY_VAULT_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(keyVaultUri))
        {
            builder.Configuration.AddAzureKeyVault(
                new Uri(keyVaultUri),
                new DefaultAzureCredential());
        }
    }
}
