using FluentAssertions;
using NUnit.Framework;

namespace RemSolution.Application.UnitTests.Common;

/// <summary>
/// Convention test for the multi-tenancy rule: cross-tenant reads via
/// IgnoreQueryFilters() are allowed only in the marketplace search feature
/// (cross-agency by design) and in DeleteAgencyCommand (a platform-admin
/// referential-integrity check) — never in agency-facing handlers.
/// </summary>
public class TenantEnforcementTests
{
    private static readonly string[] AllowedPathFragments =
    {
        Normalize("Features/MarketplaceSearch/"),
        Normalize("Features/Agency/Commands/DeleteAgencyCommand/DeleteAgencyCommand.cs"),
    };

    [Test]
    public void IgnoreQueryFiltersIsOnlyUsedInAllowedLocations()
    {
        var applicationRoot = Path.Combine(FindSolutionRoot(), "src", "Application");

        var offenders = Directory
            .EnumerateFiles(applicationRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => File.ReadAllText(f).Contains("IgnoreQueryFilters"))
            .Where(f => !AllowedPathFragments.Any(allowed => Normalize(f).Contains(allowed)))
            .ToList();

        offenders.Should().BeEmpty(
            "IgnoreQueryFilters() bypasses tenant isolation and is only allowed in MarketplaceSearch and DeleteAgencyCommand");
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
