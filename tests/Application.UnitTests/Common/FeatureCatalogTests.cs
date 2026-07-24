using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.UnitTests.Common;

/// <summary>
/// Pins the single source of truth: every permission belongs to exactly one
/// feature, and the catalog only references known features/permissions. Guards
/// against adding a permission or feature without wiring it into the catalog
/// (which drives SPA menus, staff-grant validation and effective-permission
/// computation).
/// </summary>
public class FeatureCatalogTests
{
    [Test]
    public void EveryPermissionMapsToExactlyOneFeature()
    {
        foreach (var permission in Permissions.All)
        {
            FeatureCatalog.FeatureOf(permission)
                .Should().NotBeNull($"permission '{permission}' must be assigned to a feature in FeatureCatalog");
        }
    }

    [Test]
    public void CatalogReferencesOnlyKnownFeatures()
    {
        foreach (var feature in FeatureCatalog.PermissionsByFeature.Keys)
        {
            FeatureFlags.All.Should().Contain(feature);
        }
    }

    [Test]
    public void CatalogReferencesOnlyKnownPermissions()
    {
        foreach (var permission in FeatureCatalog.PermissionsByFeature.Values.SelectMany(x => x))
        {
            Permissions.All.Should().Contain(permission);
        }
    }

    [Test]
    public void EveryFeatureIsInTheCatalog()
    {
        foreach (var feature in FeatureFlags.All)
        {
            FeatureCatalog.PermissionsByFeature.Keys.Should().Contain(feature,
                "every feature needs a catalog entry (empty permissions is fine for capability-only features)");
        }
    }

    [Test]
    public void ReadOnlyPermissionsAreAllKnown()
    {
        Permissions.ReadOnly.Should().OnlyContain(p => Permissions.All.Contains(p));
    }
}
