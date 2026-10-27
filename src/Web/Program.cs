using Hangfire;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using RemSolution.Application.Common.Files;
using RemSolution.Domain.Constants;
using RemSolution.Infrastructure;
using RemSolution.Infrastructure.Localization;
using RemSolution.Infrastructure.Data;
using RemSolution.Infrastructure.Jobs;
using RemSolution.Infrastructure.Pricing;
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

    // FluentValidation resolves {PropertyName} from the C# property name; point
    // it at the shared resources so validation messages are translated end to
    // end. Global static state, so it is configured once at startup.
    FluentValidationLocalization.Configure(
        app.Services.GetRequiredService<IStringLocalizer<SharedResource>>());

    // Migrate on a developer machine, check the deployment's bundle ran anywhere
    // else. See DatabaseOptions.
    await app.InitialiseDatabaseAsync();

    // Configure the HTTP request pipeline.

    // First, so it wraps EVERY middleware below and not just endpoint execution:
    // the impersonation, password-change and localization middleware all throw,
    // and registered last this handler never saw them — those became bare 500s
    // instead of the RFC-7807 body CustomExceptionHandler produces.
    app.UseExceptionHandler(options => { });

    if (!app.Environment.IsDevelopment())
    {
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();

        // Behind the App Service front end the socket peer is the ingress, so
        // without this every caller shares one IP: the rate limiter would
        // partition the whole internet together and the logs would record the
        // proxy. ForwardLimit 1 takes only the hop our own front end added, and
        // the known-proxy lists must be cleared because its address is not
        // stable or documented.
        var forwardedHeaders = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1,
        };

        forwardedHeaders.KnownIPNetworks.Clear();
        forwardedHeaders.KnownProxies.Clear();

        app.UseForwardedHeaders(forwardedHeaders);
    }

    // Early, so the headers reach every response — static files and refusals too.
    app.UseMiddleware<SecurityHeadersMiddleware>();

    app.UseHealthChecks("/health");
    app.UseHttpsRedirection();

    var uploadedFiles = app.Services.GetRequiredService<UploadedFiles>();
    var uploadsPath = uploadedFiles.BasePath;

    // wwwroot, minus two folders it happens to contain. The uploads default to
    // wwwroot/uploads, and serving them from here would walk straight past the
    // gate below. The NSwag build writes the API description to wwwroot/api,
    // which outside development is a map of every endpoint for anyone to read.
    app.UseWhen(
        context => !context.Request.Path.StartsWithSegments(uploadsPath) &&
                   (app.Environment.IsDevelopment() || !context.Request.Path.StartsWithSegments("/api")),
        wwwroot => wwwroot.UseStaticFiles());

    // Authentication must run before the request-context middleware so the
    // identity claims (UserId, AgencyId) are available to enrich the logs.
    app.UseAuthentication();

    // Read-only platform-admin tenant impersonation: must sit after
    // authentication (needs the principal) and before authorization (so the
    // ambient tenant + impersonation flag are live when endpoint policies run).
    app.UseMiddleware<PlatformAdminImpersonationMiddleware>();

    // After authentication so a signed-in caller is limited as a person rather
    // than as an address, and after the static-file/health middleware above so
    // SPA assets and probes never consume a permit.
    app.UseRemSolutionRateLimiter();

    // Uploaded files, from wherever FileStorage:RootPath points — on a deployment
    // that is a persistent path outside wwwroot, which a zip deploy would replace.
    // A branch of its own: after authentication, because who is asking decides
    // what they may open (see UploadsAuthorizationMiddleware), and after the rate
    // limiter, which waves car photos through and counts the rest. Ends in a 404
    // rather than falling through to the SPA, which would answer a missing
    // document with index.html.
    //
    // Only the types an upload may be, whatever is on disk: an .svg, .html or
    // .js file served from our own origin would run script with the viewer's
    // session, so anything else is not served at all.
    var uploadTypes = new FileExtensionContentTypeProvider();
    uploadTypes.Mappings.Clear();
    foreach (var (extension, contentType) in UploadFileTypes.All)
        uploadTypes.Mappings[extension] = contentType;

    app.Map(uploadsPath, uploads =>
    {
        // The branch ends before the main pipeline's logging, so it gets its own:
        // the gate's audit rows need the correlation id, and its refusals (a
        // counted contract number) belong in the request log.
        uploads.UseMiddleware<RequestContextLoggingMiddleware>();
        uploads.UseSerilogRequestLogging();
        uploads.UseMiddleware<UploadsAuthorizationMiddleware>();
        uploads.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = uploadedFiles.Files,
            ContentTypeProvider = uploadTypes,
        });
        uploads.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });
    });

    // After authentication so the signed-in user's PreferredLanguage claim can
    // outrank the culture cookie, and before anything that produces user-facing
    // text (authorization failures, validation, Razor pages).
    app.UseRequestLocalization(RequestLocalizationSetup.Build());

    app.UseAuthorization();

    // After authorization so it is the last word on an otherwise-permitted API
    // call, and after UseRequestLocalization so its problem response is in the
    // caller's language. Deliberately before the endpoints: an account still
    // holding a mailed temporary password may do nothing but replace it.
    app.UseMiddleware<PasswordChangeRequiredMiddleware>();

    app.UseMiddleware<RequestContextLoggingMiddleware>();

    // Emits one enriched summary event per HTTP request; sits inside the
    // request-context scope so it inherits CorrelationId/UserId/AgencyId.
    app.UseSerilogRequestLogging();

    // Development only. In production it would publish a map of every endpoint,
    // and nobody outside this team calls the API; the SPA's client is generated
    // at build time.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwaggerUi(settings =>
        {
            settings.Path = "/api";
            settings.DocumentPath = "/api/specification.json";
        });
    }

    // Hangfire dashboard, platform-admin only. Mapped only when Hangfire is
    // registered (skipped for the NSwag build-time host / tests), so JobStorage
    // presence gates it.
    if (app.Services.GetService<JobStorage>() is not null)
    {
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new HangfireDashboardAuthorizationFilter() }
        });

        // Lapse stale reservation holds hourly. Registered only where Hangfire
        // has real storage (not the NSwag build host or tests).
        RecurringJob.AddOrUpdate<ReservationExpiryJob>(
            "reservation-expiry", job => job.RunAsync(), Cron.Hourly());

        // Look for things worth telling people about — due maintenance and
        // papers, late hires, upcoming pickups, client reminders. Hourly rather
        // than daily so a hire that goes late in the morning is not sat on until
        // tomorrow; every alert is deduplicated by time bucket, so the extra
        // passes cost a few queries and send nothing twice.
        RecurringJob.AddOrUpdate<NotificationSweepJob>(
            "notification-sweep", job => job.RunAsync(), Cron.Hourly());

        // Apply each agency's personal-data retention rule. Daily, not hourly:
        // the window is measured in months, and this is the one sweep that
        // destroys data rather than reporting on it.
        RecurringJob.AddOrUpdate<PersonalDataPurgeJob>(
            "personal-data-purge", job => job.RunAsync(), Cron.Daily());

        // Refresh the marketplace's display rates. At 03:00 UTC, not midnight:
        // the feed publishes its own daily update just after 00:00 UTC, so a job
        // at midnight would faithfully fetch yesterday's figures.
        if (app.Services.GetRequiredService<IOptions<ExchangeRateProviderOptions>>().Value.Enabled)
        {
            RecurringJob.AddOrUpdate<ExchangeRateRefreshJob>(
                "exchange-rate-refresh", job => job.RunAsync(), Cron.Daily(3));
        }
        else
        {
            // Registered before and switched off since: leave nothing behind for
            // Hangfire to keep firing.
            RecurringJob.RemoveIfExists("exchange-rate-refresh");
        }
    }

    app.MapRazorPages();

    // Language switch for the anonymous, server-rendered Identity pages, which
    // have no SPA to call the authenticated me/language endpoint. Signed-in
    // users go through that endpoint instead, which also persists the choice.
    app.MapGet("/culture/set", (HttpContext http, string language, string? returnUrl) =>
    {
        if (!Languages.IsSupported(language))
            return Results.BadRequest();

        CultureCookie.Write(http.Response, language);

        // Local-only redirect: an open redirect here would be a phishing vector.
        var target = !string.IsNullOrEmpty(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
            ? returnUrl
            : "/";

        return Results.LocalRedirect(target);
    }).AllowAnonymous();

    app.MapFallbackToFile("index.html");

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
