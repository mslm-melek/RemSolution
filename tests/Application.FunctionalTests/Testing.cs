using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Infrastructure.Data;
using RemSolution.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace RemSolution.Application.FunctionalTests;

[SetUpFixture]
public partial class Testing
{
    private static ITestDatabase _database = null!;
    private static CustomWebApplicationFactory _factory = null!;
    private static IServiceScopeFactory _scopeFactory = null!;
    private static string? _userId;
    private static int? _agencyId;

    // Isolated per-run file-storage root so upload tests never write into a
    // real wwwroot and leave nothing behind (deleted in teardown).
    public static string UploadsRoot { get; } =
        Path.Combine(Path.GetTempPath(), "RemSolutionTests", $"uploads-{Guid.NewGuid():N}");

    [OneTimeSetUp]
    public async Task RunBeforeAnyTests()
    {
        _database = await TestDatabaseFactory.CreateAsync();

        _factory = new CustomWebApplicationFactory(_database.GetConnection(), _database.GetConnectionString());

        _scopeFactory = _factory.Services.GetRequiredService<IServiceScopeFactory>();
    }

    public static async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        using var scope = _scopeFactory.CreateScope();

        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

        return await mediator.Send(request);
    }

    public static async Task SendAsync(IBaseRequest request)
    {
        using var scope = _scopeFactory.CreateScope();

        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

        await mediator.Send(request);
    }

    public static string? GetUserId()
    {
        return _userId;
    }

    public static int? GetAgencyId()
    {
        return _agencyId;
    }

    public static void SetCurrentAgency(int? agencyId)
    {
        _agencyId = agencyId;
    }

    public static async Task<string> RunAsDefaultUserAsync()
    {
        return await RunAsUserAsync("test@local", "Testing1234!", Array.Empty<string>());
    }

    public static async Task<string> RunAsPlatformAdministratorAsync()
    {
        return await RunAsUserAsync("platformadmin@local", "PlatformAdmin1234!", new[] { Roles.PlatformAdministrator });
    }

    public static async Task<string> RunAsAgencyAdministratorAsync()
    {
        return await RunAsUserAsync("agencyadmin@local", "AgencyAdmin1234!", new[] { Roles.AgencyAdministrator });
    }

    // Staff capability is the set of permission grants, not the role: pass
    // exactly the permissions the test needs (none to test denial).
    public static async Task<string> RunAsAgencyStaffAsync(params string[] permissions)
    {
        var userId = await RunAsUserAsync("agencystaff@local", "AgencyStaff1234!", new[] { Roles.AgencyStaff });

        foreach (var permission in permissions)
        {
            await AddAsync(new UserPermission { UserId = userId, Permission = permission });
        }

        return userId;
    }

    public static async Task<string> RunAsUserAsync(string userName, string password, string[] roles)
    {
        using var scope = _scopeFactory.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser { UserName = userName, Email = userName };

        var result = await userManager.CreateAsync(user, password);

        if (roles.Any())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Production seeds the full role set at startup; the per-test DB
            // reset wipes it, so restore it whenever a role-carrying user is
            // needed — handlers may assign roles beyond the caller's own
            // (e.g. CreateAgencyUser adds AgencyStaff).
            var standardRoles = new[] { Roles.PlatformAdministrator, Roles.AgencyAdministrator, Roles.AgencyStaff };

            foreach (var role in standardRoles.Union(roles))
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            await userManager.AddToRolesAsync(user, roles);
        }

        if (result.Succeeded)
        {
            _userId = user.Id;

            return _userId;
        }

        var errors = string.Join(Environment.NewLine, result.ToApplicationResult().Errors);

        throw new Exception($"Unable to create {userName}.{Environment.NewLine}{errors}");
    }

    public static async Task<bool> IsInRoleAsync(string userId, string role)
    {
        using var scope = _scopeFactory.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByIdAsync(userId);

        return user is not null && await userManager.IsInRoleAsync(user, role);
    }

    public static async Task ResetState()
    {
        try
        {
            await _database.ResetAsync();
        }
        catch (Exception)
        {
        }

        // The settings cache is a singleton that outlives the DB reset; clear it
        // so a cached snapshot never leaks into the next test (which may reuse
        // the same agency id).
        using (var scope = _scopeFactory.CreateScope())
        {
            if (scope.ServiceProvider.GetService<IMemoryCache>() is MemoryCache memoryCache)
            {
                memoryCache.Clear();
            }
        }

        _userId = null;
        _agencyId = null;
    }

    public static async Task<TEntity?> FindAsync<TEntity>(params object[] keyValues)
        where TEntity : class
    {
        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.FindAsync<TEntity>(keyValues);
    }

    // Fetches a row bypassing global query filters (tenant + soft-delete), for
    // asserting on archived rows that normal reads hide.
    public static async Task<TEntity?> FindIgnoringFiltersAsync<TEntity>(
        System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate)
        where TEntity : class
    {
        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.Set<TEntity>().IgnoreQueryFilters().FirstOrDefaultAsync(predicate);
    }

    public static async Task AddAsync<TEntity>(TEntity entity)
        where TEntity : class
    {
        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        context.Add(entity);

        await context.SaveChangesAsync();
    }

    // Tenant entities require an existing Agency (which itself requires a Country).
    // Also makes that agency the current tenant, so tenant query filters and
    // AgencyId stamping apply to everything the test does afterwards.
    // Tenant writes require an active subscription, so one is provisioned by
    // default (generous limits keep unrelated tests unaffected); pass
    // withSubscription: false to test the unsubscribed state.
    public static async Task<int> AddTestAgencyAsync(int maxCars = 100, int maxClients = 100, int maxUsers = 100, bool withSubscription = true)
    {
        var country = new Country { Name = "Testland" };
        await AddAsync(country);

        var agency = new Agency
        {
            Name = "Test Agency",
            CountryId = country.Id,
            // Settings row (currency etc.) is required now that it lives off-Agency.
            Settings = new AgencySettings { CurrencyCode = "TND" }
        };
        await AddAsync(agency);

        if (withSubscription)
        {
            var plan = new SubscriptionPlan
            {
                Name = $"Test Plan {maxCars}/{maxClients}/{maxUsers}",
                MaxCars = maxCars,
                MaxClients = maxClients,
                MaxUsers = maxUsers,
                Price = 49.99m
            };
            await AddAsync(plan);

            await AddAsync(new AgencySubscription
            {
                AgencyId = agency.Id,
                PlanId = plan.Id,
                StartDate = DateTimeOffset.UtcNow.AddDays(-1),
                EndDate = DateTimeOffset.UtcNow.AddDays(30),
                Status = SubscriptionStatus.Active
            });
        }

        _agencyId = agency.Id;

        return agency.Id;
    }

    public static async Task MutateSubscriptionAsync(int agencyId, Action<AgencySubscription> mutate)
    {
        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var subscriptions = await context.AgencySubscriptions
            .Where(s => s.AgencyId == agencyId)
            .ToListAsync();

        subscriptions.ForEach(mutate);

        await context.SaveChangesAsync();
    }

    public static string GetConnectionString()
    {
        return _database.GetConnectionString();
    }

    public static async Task UpdateAsync<TEntity>(TEntity entity)
        where TEntity : class
    {
        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        context.Update(entity);

        await context.SaveChangesAsync();
    }

    public static async Task<int> CountAsync<TEntity>() where TEntity : class
    {
        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.Set<TEntity>().CountAsync();
    }

    public static async Task<int> CountAsync<TEntity>(System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate) where TEntity : class
    {
        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.Set<TEntity>().CountAsync(predicate);
    }

    /// <summary>
    /// Runs a delegate against a fresh service scope — for tests that exercise
    /// a service directly instead of going through a MediatR request.
    /// </summary>
    public static async Task<T> UsingScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = _scopeFactory.CreateScope();

        return await action(scope.ServiceProvider);
    }

    [OneTimeTearDown]
    public async Task RunAfterAnyTests()
    {
        await _database.DisposeAsync();
        await _factory.DisposeAsync();

        if (Directory.Exists(UploadsRoot))
            Directory.Delete(UploadsRoot, recursive: true);
    }
}
