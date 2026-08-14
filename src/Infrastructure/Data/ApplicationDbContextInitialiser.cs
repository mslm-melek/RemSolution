using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RemSolution.Infrastructure.Data;

public static class InitialiserExtensions
{
    public static void AddAsyncSeeding(this DbContextOptionsBuilder builder, IServiceProvider serviceProvider)
    {
        builder.UseAsyncSeeding(async (context, _, ct) =>
        {
            var initialiser = serviceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

            await initialiser.SeedAsync();
        });

        // Sync counterpart required by synchronous operations (e.g. `dotnet ef database update`).
        builder.UseSeeding((context, _) =>
        {
            var initialiser = serviceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

            initialiser.SeedAsync().GetAwaiter().GetResult();
        });
    }

    /// <summary>
    /// Migrates or checks the schema (see <see cref="DatabaseOptions"/>), then
    /// seeds the reference data. Called in every environment; configuration
    /// decides what it does.
    /// </summary>
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        // The NSwag build-time host has no database behind it.
        if (app.Configuration.GetConnectionString("RemSolutionDb")
            == DatabaseOptions.BuildTimePlaceholderConnectionString)
        {
            return;
        }

        using var scope = app.Services.CreateScope();

        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var development = app.Environment.IsDevelopment();

        // Migrating from the app is a developer-machine default; elsewhere the
        // deployment applies the bundle.
        if (options.MigrateOnStartup ?? development)
        {
            await initialiser.MigrateAsync();
        }
        else
        {
            await initialiser.EnsureSchemaIsCurrentAsync(options.FailOnPendingMigrations);
        }

        if (options.SeedOnStartup)
        {
            await initialiser.SeedAsync();
        }

        // Demo data is doubly gated: the DemoData:Enabled flag AND the Development
        // environment. Migrating a database is safe anywhere; filling it with fake
        // bookings is not, and this method is the one place both facts are known.
        var demoData = scope.ServiceProvider.GetRequiredService<IOptions<DemoDataOptions>>().Value;

        if (demoData.Enabled && development)
        {
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
        }
    }
}

public class ApplicationDbContextInitialiser
{
    // Instances may start together, and the seed is idempotent but not atomic, so
    // it is serialised on a named app lock. Session-owned: the seed opens its own
    // transactions.
    private const string SeedLockResource = "remsolution-reference-data-seed";

    private readonly ILogger<ApplicationDbContextInitialiser> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly DatabaseOptions _options;
    private readonly IHostEnvironment _environment;

    public ApplicationDbContextInitialiser(
        ILogger<ApplicationDbContextInitialiser> logger,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<DatabaseOptions> options,
        IHostEnvironment environment)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _options = options.Value;
        _environment = environment;
    }

    /// <summary>
    /// Applies pending migrations; only called where that is allowed (see
    /// <see cref="DatabaseOptions.MigrateOnStartup"/>).
    /// </summary>
    public async Task MigrateAsync()
    {
        try
        {
            await _context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while migrating the database.");
            throw;
        }
    }

    /// <summary>
    /// Verifies the deployment already moved the schema forward. With
    /// <paramref name="fail"/> the host refuses to start, which is easier to
    /// diagnose than missing-column errors one endpoint at a time.
    /// </summary>
    public async Task EnsureSchemaIsCurrentAsync(bool fail)
    {
        var pending = (await _context.Database.GetPendingMigrationsAsync()).ToList();

        if (pending.Count == 0)
        {
            return;
        }

        _logger.LogCritical(
            "The database is missing {Count} migration(s): {Migrations}. Apply the migration bundle as part of the deployment (see docs/RUNBOOK_Base_De_Donnees.md).",
            pending.Count, string.Join(", ", pending));

        if (fail)
        {
            throw new InvalidOperationException(
                $"The database is behind this build by {pending.Count} migration(s): {string.Join(", ", pending)}. " +
                "Apply the migration bundle before starting the application, or set Database:FailOnPendingMigrations to false to start anyway.");
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            // Held for the whole seed, released with the connection; a second
            // instance waits here and then finds nothing left to do.
            await using var connection = new Microsoft.Data.SqlClient.SqlConnection(
                _context.Database.GetConnectionString());

            await connection.OpenAsync();

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
DECLARE @result int;
EXEC @result = sp_getapplock
    @Resource = @resource,
    @LockMode = 'Exclusive',
    @LockOwner = 'Session',
    @LockTimeout = 120000;
IF @result < 0 THROW 51000, 'Failed to acquire the reference-data seed lock.', 1;";
                command.Parameters.AddWithValue("@resource", SeedLockResource);

                await command.ExecuteNonQueryAsync();
            }

            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    public async Task TrySeedAsync()
    {
        // Default roles
        foreach (var roleName in new[] { Roles.PlatformAdministrator, Roles.AgencyAdministrator, Roles.AgencyStaff, Roles.Customer })
        {
            if (_roleManager.Roles.All(r => r.Name != roleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // One platform administrator. Deliberately no AgencyId: platform admins
        // are not tenant-scoped and must never carry the AgencyId claim.
        var login = _options.PlatformAdminEmail;

        if (_userManager.Users.All(u => u.UserName != login))
        {
            // No configured password outside Development means no account: a
            // well-known password in the binary would be a published back door.
            var password = _options.PlatformAdminPassword;

            if (string.IsNullOrWhiteSpace(password) && _environment.IsDevelopment())
            {
                password = "PlatformAdmin1!";
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning(
                    "No platform administrator exists and Database:PlatformAdminPassword is not configured, so none was created. Set it (Key Vault or an environment variable), restart once to create '{Login}', then sign in and change the password.",
                    login);
            }
            else
            {
                var created = await _userManager.CreateAsync(
                    new ApplicationUser
                    {
                        UserName = login,
                        Email = login,
                        // Whoever set the password is not necessarily its user.
                        MustChangePassword = !_environment.IsDevelopment(),
                    },
                    password);

                if (!created.Succeeded)
                {
                    _logger.LogError(
                        "Could not create the platform administrator '{Login}': {Errors}",
                        login, string.Join("; ", created.Errors.Select(e => e.Description)));
                }
            }
        }

        // Role assignment is done outside the creation branch so databases seeded
        // before a role existed still pick it up.
        var platformAdminUser = await _userManager.FindByNameAsync(login);

        if (platformAdminUser is not null && !await _userManager.IsInRoleAsync(platformAdminUser, Roles.PlatformAdministrator))
        {
            await _userManager.AddToRoleAsync(platformAdminUser, Roles.PlatformAdministrator);
        }

        // Countries are reference data: seeded once, no UI needed to maintain them.
        if (!await _context.Countries.AnyAsync())
        {
            var countryNames = new[]
            {
                "Afghanistan", "Afrique du Sud", "Albanie", "Algérie", "Allemagne", "Andorre", "Angola",
                "Antigua-et-Barbuda", "Arabie saoudite", "Argentine", "Arménie", "Australie", "Autriche",
                "Azerbaïdjan", "Bahamas", "Bahreïn", "Bangladesh", "Barbade", "Belgique", "Belize", "Bénin",
                "Bhoutan", "Biélorussie", "Birmanie", "Bolivie", "Bosnie-Herzégovine", "Botswana", "Brésil",
                "Brunei", "Bulgarie", "Burkina Faso", "Burundi", "Cambodge", "Cameroun", "Canada", "Cap-Vert",
                "Chili", "Chine", "Chypre", "Colombie", "Comores", "Congo", "Corée du Nord", "Corée du Sud",
                "Costa Rica", "Côte d'Ivoire", "Croatie", "Cuba", "Danemark", "Djibouti", "Dominique",
                "Égypte", "Émirats arabes unis", "Équateur", "Érythrée", "Espagne", "Estonie", "Eswatini",
                "États-Unis", "Éthiopie", "Fidji", "Finlande", "France", "Gabon", "Gambie", "Géorgie",
                "Ghana", "Grèce", "Grenade", "Guatemala", "Guinée", "Guinée-Bissau", "Guinée équatoriale",
                "Guyana", "Haïti", "Honduras", "Hongrie", "Îles Marshall", "Îles Salomon", "Inde",
                "Indonésie", "Irak", "Iran", "Irlande", "Islande", "Israël", "Italie", "Jamaïque", "Japon",
                "Jordanie", "Kazakhstan", "Kenya", "Kirghizistan", "Kiribati", "Koweït", "Laos", "Lesotho",
                "Lettonie", "Liban", "Liberia", "Libye", "Liechtenstein", "Lituanie", "Luxembourg",
                "Macédoine du Nord", "Madagascar", "Malaisie", "Malawi", "Maldives", "Mali", "Malte",
                "Maroc", "Maurice", "Mauritanie", "Mexique", "Micronésie", "Moldavie", "Monaco", "Mongolie",
                "Monténégro", "Mozambique", "Namibie", "Nauru", "Népal", "Nicaragua", "Niger", "Nigeria",
                "Norvège", "Nouvelle-Zélande", "Oman", "Ouganda", "Ouzbékistan", "Pakistan", "Palaos",
                "Palestine", "Panama", "Papouasie-Nouvelle-Guinée", "Paraguay", "Pays-Bas", "Pérou",
                "Philippines", "Pologne", "Portugal", "Qatar", "République centrafricaine",
                "République démocratique du Congo", "République dominicaine", "République tchèque",
                "Roumanie", "Royaume-Uni", "Russie", "Rwanda", "Saint-Christophe-et-Niévès", "Saint-Marin",
                "Saint-Vincent-et-les-Grenadines", "Sainte-Lucie", "Salvador", "Samoa",
                "Sao Tomé-et-Principe", "Sénégal", "Serbie", "Seychelles", "Sierra Leone", "Singapour",
                "Slovaquie", "Slovénie", "Somalie", "Soudan", "Soudan du Sud", "Sri Lanka", "Suède",
                "Suisse", "Suriname", "Syrie", "Tadjikistan", "Tanzanie", "Tchad", "Thaïlande",
                "Timor oriental", "Togo", "Tonga", "Trinité-et-Tobago", "Tunisie", "Turkménistan",
                "Turquie", "Tuvalu", "Ukraine", "Uruguay", "Vanuatu", "Vatican", "Venezuela", "Vietnam",
                "Yémen", "Zambie", "Zimbabwe"
            };

            _context.Countries.AddRange(countryNames.Select(name => new Country { Name = name }));

            await _context.SaveChangesAsync();
        }

        // Subscription plans + their feature entitlements. Features are unlocked
        // per plan under the allow-list model, so at least one plan must exist.
        if (!await _context.SubscriptionPlans.AnyAsync())
        {
            var starter = new SubscriptionPlan
            {
                Name = "Starter",
                MaxCars = 10,
                MaxClients = 50,
                MaxUsers = 3,
                Price = 0m,
                Features = new[]
                    {
                        FeatureFlags.Cars, FeatureFlags.Clients, FeatureFlags.Branches,
                        FeatureFlags.Rentings, FeatureFlags.Reservations,
                    }
                    .Select(f => new PlanFeature { Feature = f })
                    .ToList()
            };

            var full = new SubscriptionPlan
            {
                Name = "Full",
                MaxCars = 1000,
                MaxClients = 5000,
                MaxUsers = 100,
                Price = 299m,
                Features = FeatureFlags.All.Select(f => new PlanFeature { Feature = f }).ToList()
            };

            _context.SubscriptionPlans.AddRange(starter, full);

            await _context.SaveChangesAsync();
        }

        // Backfill: under the allow-list model an agency with no active
        // subscription loses every module. Give any agency lacking a current
        // subscription the most generous plan so existing agencies keep working.
        var fullPlan = await _context.SubscriptionPlans
            .OrderByDescending(p => p.Price)
            .FirstOrDefaultAsync();

        if (fullPlan is not null)
        {
            var now = DateTimeOffset.UtcNow;
            var agencyIds = await _context.Agencies.Select(a => a.Id).ToListAsync();

            foreach (var agencyId in agencyIds)
            {
                var hasActive = await _context.AgencySubscriptions
                    .AnyAsync(AgencySubscription.IsActiveFor(agencyId, now));

                if (!hasActive)
                {
                    _context.AgencySubscriptions.Add(new AgencySubscription
                    {
                        AgencyId = agencyId,
                        PlanId = fullPlan.Id,
                        Status = SubscriptionStatus.Active,
                        StartDate = now,
                        EndDate = now.AddYears(100)
                    });
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
