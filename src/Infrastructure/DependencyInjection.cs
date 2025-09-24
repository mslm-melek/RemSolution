using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Constants;
using RemSolution.Infrastructure.Data;
using RemSolution.Infrastructure.Data.Interceptors;
using RemSolution.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        //var connectionString = builder.Configuration.GetConnectionString("RemSolutionDb");
        var connectionString = "Server=localhost;Database=RemSolutionDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";

        Guard.Against.Null(connectionString, message: "Connection string 'RemSolutionDb' not found.");

        builder.Services.AddScoped<ISaveChangesInterceptor, BaseEntityInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(connectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)).AddAsyncSeeding(sp);
        ;
    });


        builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        builder.Services.AddScoped<ApplicationDbContextInitialiser>();

        builder.Services
            .AddDefaultIdentity<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddTransient<IIdentityService, IdentityService>();

        builder.Services.AddAuthorization(options =>
            options.AddPolicy(Policies.CanPurge, policy => policy.RequireRole(Roles.Administrator)));
    }
}
