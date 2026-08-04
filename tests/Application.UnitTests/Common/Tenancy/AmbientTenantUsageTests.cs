using FluentAssertions;
using NUnit.Framework;

namespace RemSolution.Application.UnitTests.Common.Tenancy;

/// <summary>
/// Convention test pinning the tenant-isolation-bypass surface that is NOT
/// covered by <c>TenantEnforcementTests</c> (which pins IgnoreQueryFilters).
/// Pushing an ambient tenant (<c>AmbientTenant.Push</c>) makes code act as a
/// given agency, and <c>ImpersonationScope</c> unlocks the platform-admin
/// read-only permission bypass — both must appear only in sanctioned locations.
/// </summary>
public class AmbientTenantUsageTests
{
    // Where acting-as-a-tenant is legitimate: the background workers (image
    // processing, reservation expiry), the read-only impersonation middleware,
    // and the platform-admin handlers that manage or report on a specific agency.
    private static readonly string[] AmbientTenantAllowed =
    {
        Normalize("Infrastructure/Imaging/CarImageProcessingJob.cs"),
        Normalize("Infrastructure/Jobs/ReservationExpiryJob.cs"),
        // Same shape as the expiry sweep: it has to look at every agency's fleet
        // and bookings, and does it one tenant at a time under a push rather than
        // by bypassing the query filter.
        Normalize("Infrastructure/Jobs/NotificationSweepJob.cs"),
        // The demo seeder writes each sample agency's data in turn, so it acts
        // as every tenant by design.
        Normalize("Infrastructure/Data/DemoDataSeeder.cs"),
        // Customer marketplace commands act as the car's agency to create/cancel
        // a hold, and to post/read the customer's side of a renting's chat.
        Normalize("Features/Marketplace/Commands/"),
        Normalize("Web/Middleware/PlatformAdminImpersonationMiddleware.cs"),
        Normalize("Features/Users/Commands/CreateAgencyUserByAdminCommand/"),
        Normalize("Features/Agency/Queries/GetAgencyFeaturesQuery/"),
        Normalize("Features/Agency/Commands/SetAgencyFeatureCommand/"),
        Normalize("Features/AgencySubscription/Queries/GetAgencyUsageQuery/"),
        // Reading one agency's branches (an ITenantEntity) while the platform
        // administrator edits that agency.
        Normalize("Features/Agency/Queries/GetAgencyBranchesQuery/"),
    };

    // The administrative push additionally exempts its writes from subscription
    // enforcement, so it is pinned separately and more tightly: only the
    // platform-admin handlers that set an agency's branches up, where the agency
    // either has no subscription yet or has one that lapsed.
    private static readonly string[] AmbientTenantAdministrativeAllowed =
    {
        Normalize("Features/Agency/Commands/CreateAgencyCommand/"),
        Normalize("Features/Agency/Commands/CreateAgencyBranchCommand/"),
        Normalize("Features/Agency/Commands/UpdateAgencyBranchCommand/"),
        Normalize("Features/Agency/Commands/DeleteAgencyBranchCommand/"),
        // Where the exemption is read.
        Normalize("Infrastructure/Data/Interceptors/SubscriptionEnforcementInterceptor.cs"),
    };

    // The impersonation flag is set only by the middleware, and read only by the
    // authorization policy and the current-user endpoint (which has to report the
    // same effective permissions the policy will enforce).
    private static readonly string[] ImpersonationScopeAllowed =
    {
        Normalize("Infrastructure/DependencyInjection.cs"),
        Normalize("Web/Middleware/PlatformAdminImpersonationMiddleware.cs"),
        Normalize("Web/Endpoints/Users.cs"),
    };

    [Test]
    public void AmbientTenantPushIsOnlyUsedInAllowedLocations()
    {
        var offenders = EnumerateSourceFiles()
            .Where(f => File.ReadAllText(f).Contains("AmbientTenant.Push("))
            .Where(f => !AmbientTenantAllowed.Any(allowed => Normalize(f).Contains(allowed)))
            .ToList();

        offenders.Should().BeEmpty(
            "AmbientTenant.Push acts as another tenant and is only allowed in the image job, the impersonation middleware, and the platform-admin agency handlers");
    }

    [Test]
    public void AmbientTenantPushAdministrativeIsOnlyUsedInAllowedLocations()
    {
        var offenders = EnumerateSourceFiles()
            .Where(f =>
            {
                var source = File.ReadAllText(f);
                return source.Contains("AmbientTenant.PushAdministrative(") ||
                       source.Contains("AmbientTenant.CurrentIsAdministrative");
            })
            .Where(f => !f.EndsWith(Normalize("Application/Common/Tenancy/AmbientTenant.cs")))
            .Where(f => !AmbientTenantAdministrativeAllowed.Any(allowed => Normalize(f).Contains(allowed)))
            .ToList();

        offenders.Should().BeEmpty(
            "PushAdministrative acts as another tenant AND exempts the write from subscription enforcement, so it is only allowed in the platform-admin handlers that set an agency's branches up");
    }

    [Test]
    public void ImpersonationScopeIsOnlyReferencedInAllowedLocations()
    {
        var offenders = EnumerateSourceFiles()
            .Where(f => File.ReadAllText(f).Contains("ImpersonationScope."))
            .Where(f => !ImpersonationScopeAllowed.Any(allowed => Normalize(f).Contains(allowed)))
            .ToList();

        offenders.Should().BeEmpty(
            "ImpersonationScope gates the platform-admin permission bypass and is only referenced by the authorization policy, the impersonation middleware and the current-user endpoint");
    }

    private static IEnumerable<string> EnumerateSourceFiles()
    {
        var sourceRoots = new[]
        {
            Path.Combine(FindSolutionRoot(), "src", "Application"),
            Path.Combine(FindSolutionRoot(), "src", "Infrastructure"),
            Path.Combine(FindSolutionRoot(), "src", "Web"),
        };

        return sourceRoots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
    }

    private static string Normalize(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "RemSolution.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Solution root not found.");
    }
}
