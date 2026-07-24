using Hangfire;
using RemSolution.Infrastructure.Data;
using RemSolution.Web.Infrastructure;
using RemSolution.Web.Middleware;
using Serilog;

// Bootstrap logger: captures failures during host build/startup, before the
// configured Serilog pipeline is wired from appsettings.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Replace the default console logging with Serilog: JSON sinks + enrichers
    // configured from appsettings, plus App Insights when running in Azure.
    builder.Services.AddSerilog((services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext();

        var appInsightsConnectionString =
            builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
        {
            configuration.WriteTo.ApplicationInsights(
                new Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration
                {
                    ConnectionString = appInsightsConnectionString
                },
                new Serilog.Sinks.ApplicationInsights.TelemetryConverters.TraceTelemetryConverter());
        }
    });

    // Add services to the container.
    builder.AddKeyVaultIfConfigured();
    builder.AddApplicationServices();
    builder.AddInfrastructureServices();
    builder.AddWebServices();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        await app.InitialiseDatabaseAsync();
    }
    else
    {
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    app.UseHealthChecks("/health");
    app.UseHttpsRedirection();
    app.UseStaticFiles();

    // Authentication must run before the request-context middleware so the
    // identity claims (UserId, AgencyId) are available to enrich the logs.
    app.UseAuthentication();
    // Read-only platform-admin tenant impersonation: must sit after
    // authentication (needs the principal) and before authorization (so the
    // ambient tenant + impersonation flag are live when endpoint policies run).
    app.UseMiddleware<PlatformAdminImpersonationMiddleware>();
    app.UseAuthorization();
    app.UseMiddleware<RequestContextLoggingMiddleware>();

    // Emits one enriched summary event per HTTP request; sits inside the
    // request-context scope so it inherits CorrelationId/UserId/AgencyId.
    app.UseSerilogRequestLogging();

    app.UseSwaggerUi(settings =>
    {
        settings.Path = "/api";
        settings.DocumentPath = "/api/specification.json";
    });

    // Hangfire dashboard, platform-admin only. Mapped only when Hangfire is
    // registered (skipped for the NSwag build-time host / tests), so JobStorage
    // presence gates it.
    if (app.Services.GetService<JobStorage>() is not null)
    {
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new HangfireDashboardAuthorizationFilter() }
        });
    }

    app.MapRazorPages();

    app.MapFallbackToFile("index.html");

    app.UseExceptionHandler(options => { });


    app.MapEndpoints();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "RemSolution host terminated unexpectedly");
    throw; // Re-throw so a failed startup exits non-zero for the orchestrator/health checks.
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
