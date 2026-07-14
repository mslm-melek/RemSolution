using FluentAssertions;
using NUnit.Framework;

namespace RemSolution.Application.UnitTests.Common;

/// <summary>
/// Convention test for the cross-tenant audit rule: every request handler that
/// uses ICrossTenantAccess (the platform-admin cross-tenant read path) must be
/// marked [Auditable], so the read lands in the business audit trail alongside
/// the action it serves. CrossTenantAccess also refuses at runtime when no
/// audit scope is open; this test catches the violation at build time, with a
/// message that says how to fix it.
/// </summary>
public class CrossTenantAuditTests
{
    [Test]
    public void CrossTenantReadersMustBeAuditable()
    {
        var featuresRoot = Path.Combine(FindSolutionRoot(), "src", "Application", "Features");

        var offenders = Directory.EnumerateFiles(featuresRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(f => (Path: f, Source: File.ReadAllText(f)))
            .Where(f => f.Source.Contains("ICrossTenantAccess"))
            .Where(f => !f.Source.Contains("[Auditable"))
            .Select(f => f.Path)
            .ToList();

        offenders.Should().BeEmpty(
            "platform-admin cross-tenant reads are part of the [Auditable] trail by contract — mark the request with [Auditable(...)]");
    }

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
