using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();

        await initialiser.InitialiseAsync();
    }
}

public class ApplicationDbContextInitialiser
{
    private readonly ILogger<ApplicationDbContextInitialiser> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public ApplicationDbContextInitialiser(ILogger<ApplicationDbContextInitialiser> logger, ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task InitialiseAsync()
    {
        try
        {
            await _context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initialising the database.");
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
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
        foreach (var roleName in new[] { Roles.PlatformAdministrator, Roles.AgencyAdministrator, Roles.AgencyStaff })
        {
            if (_roleManager.Roles.All(r => r.Name != roleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // One platform administrator. Deliberately no AgencyId: platform admins
        // are not tenant-scoped and must never carry the AgencyId claim.
        var platformAdmin = new ApplicationUser { UserName = "platformadmin@localhost", Email = "platformadmin@localhost" };

        if (_userManager.Users.All(u => u.UserName != platformAdmin.UserName))
        {
            await _userManager.CreateAsync(platformAdmin, "PlatformAdmin1!");
        }

        // Role assignment is done outside the creation branch so databases seeded
        // before a role existed still pick it up.
        var platformAdminUser = await _userManager.FindByNameAsync(platformAdmin.UserName);

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
