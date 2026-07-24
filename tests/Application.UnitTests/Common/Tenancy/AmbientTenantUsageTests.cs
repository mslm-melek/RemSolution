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
    // Where acting-as-a-tenant is legitimate: the image-processing worker, the
    // read-only impersonation middleware, and the platform-admin handlers that
    // manage or report on a specific agency.
    private static readonly string[] AmbientTenantAllowed =
    {
        Normalize("Infrastructure/Imaging/CarImageProcessingJob.cs"),
        Normalize("Web/Middleware/PlatformAdminImpersonationMiddleware.cs"),
        Normalize("Features/Users/Commands/CreateAgencyUserByAdminCommand/"),
        Normalize("Features/Agency/Queries/GetAgencyFeaturesQuery/"),
        Normalize("Features/Agency/Commands/SetAgencyFeatureCommand/"),
        Normalize("Features/AgencySubscription/Queries/GetAgencyUsageQuery/"),
    };

    // The impersonation flag is read only by the authorization policy and set
    // only by the middleware.
    private static readonly string[] ImpersonationScopeAllowed =
    {
        Normalize("Infrastructure/DependencyInjection.cs"),
        Normalize("Web/Middleware/PlatformAdminImpersonationMiddleware.cs"),
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
    public void ImpersonationScopeIsOnlyReferencedInAllowedLocations()
    {
        var offenders = EnumerateSourceFiles()
            .Where(f => File.ReadAllText(f).Contains("ImpersonationScope."))
            .Where(f => !ImpersonationScopeAllowed.Any(allowed => Normalize(f).Contains(allowed)))
            .ToList();

        offenders.Should().BeEmpty(
            "ImpersonationScope gates the platform-admin read-only permission bypass and is only referenced by the authorization policy and the impersonation middleware");
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
