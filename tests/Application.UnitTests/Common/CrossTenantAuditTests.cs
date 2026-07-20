using System.Reflection;
using FluentAssertions;
using MediatR;
using NUnit.Framework;
using RemSolution.Application.Common.Audit;
using RemSolution.Application.Common.Interfaces;

namespace RemSolution.Application.UnitTests.Common;

/// <summary>
/// Convention tests for the cross-tenant audit rule: every request — command or
/// query — whose handler uses ICrossTenantAccess (the platform-admin
/// cross-tenant read path) must be marked [Auditable], so the read lands in the
/// business audit trail alongside the action it serves. CrossTenantAccess also
/// refuses at runtime when no audit scope is open; these tests catch the
/// violation at build time, with a message that says how to fix it.
///
/// Two complementary checks: a reflection pass over handler constructor
/// dependencies (robust to the request and handler living in separate files),
/// and a source scan (catches uses of ICrossTenantAccess that bypass
/// constructor injection).
/// </summary>
public class CrossTenantAuditTests
{
    [Test]
    public void HandlersUsingCrossTenantAccessMustHandleAuditableRequests()
    {
        var applicationAssembly = typeof(ICrossTenantAccess).Assembly;

        var offenders = applicationAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => t.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Any(p => p.ParameterType == typeof(ICrossTenantAccess)))
            .SelectMany(handler => handler.GetInterfaces()
                .Where(i => i.IsGenericType &&
                            (i.GetGenericTypeDefinition() == typeof(IRequestHandler<>) ||
                             i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
                .Select(i => (Handler: handler, Request: i.GetGenericArguments()[0])))
            .Where(x => x.Request.GetCustomAttribute<AuditableAttribute>() is null)
            .Select(x => $"{x.Request.Name} (handled by {x.Handler.Name})")
            .ToList();

        offenders.Should().BeEmpty(
            "platform-admin cross-tenant reads are part of the [Auditable] trail by contract — mark the request with [Auditable(...)]");
    }

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
