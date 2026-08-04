using FluentAssertions;
using NUnit.Framework;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.UnitTests.Common;

/// <summary>
/// Pins the stored form of a user's home-screen choice. The keys and their
/// serialization are a contract with rows already in AspNetUsers, so a rename or
/// a format change here silently empties every user's home screen.
/// </summary>
public class HomeWidgetsTests
{
    [Test]
    public void NeverChosenIsNotTheSameAsChoosingNothing()
    {
        HomeWidgets.Parse(null).Should().BeNull("null means the user never chose, and gets the default tiles");
        HomeWidgets.Parse(string.Empty).Should().BeEmpty("an empty string is the deliberate 'no tiles'");
    }

    [Test]
    public void RoundTripsInTheChosenOrder()
    {
        var chosen = new[] { HomeWidgets.Rentings, HomeWidgets.Cars, HomeWidgets.Credits };

        HomeWidgets.Parse(HomeWidgets.Serialize(chosen)).Should().Equal(chosen);
    }

    [Test]
    public void DropsKeysItNoLongerRecognises()
    {
        // A key retired in a later release must not break the whole selection.
        HomeWidgets.Parse($"{HomeWidgets.Cars},Antiques,{HomeWidgets.Clients}")
            .Should().Equal(HomeWidgets.Cars, HomeWidgets.Clients);
    }

    [Test]
    public void DrawsARepeatedKeyOnce()
    {
        // The command refuses duplicates, so a repeat can only come from a row
        // edited outside the app — which must still not draw the tile twice.
        HomeWidgets.Parse($"{HomeWidgets.Cars},{HomeWidgets.Cars}")
            .Should().Equal(HomeWidgets.Cars);
    }

    [Test]
    public void KnownKeysAreExactlyTheCatalog()
    {
        HomeWidgets.All.Should().OnlyHaveUniqueItems();
        HomeWidgets.All.Should().OnlyContain(key => HomeWidgets.IsKnown(key));
        HomeWidgets.IsKnown("cars").Should().BeFalse("keys are case-sensitive, as stored");
    }

    [Test]
    public void MoreTilesExistThanOneUserMayPin()
    {
        // The cap only means something while the catalog is longer than it.
        HomeWidgets.All.Count(key => !HomeWidgets.IsPanel(key))
            .Should().BeGreaterThan(HomeWidgets.MaxPinned);
    }

    [Test]
    public void PanelsAreKeysFromTheCatalog()
    {
        HomeWidgets.Panels.Should().OnlyHaveUniqueItems();
        HomeWidgets.Panels.Should().OnlyContain(key => HomeWidgets.IsKnown(key));
    }

    [Test]
    public void PanelsDoNotCountAgainstTheTileCap()
    {
        // The cap bounds the tile row; a panel renders under it and so is exempt
        // (see HomeWidgets.Panels) — a user with a full row can still add one.
        var fullRow = HomeWidgets.All.Where(key => !HomeWidgets.IsPanel(key))
            .Take(HomeWidgets.MaxPinned)
            .ToList();

        HomeWidgets.CountTiles(fullRow).Should().Be(HomeWidgets.MaxPinned);

        fullRow.Add(HomeWidgets.Calendar);

        HomeWidgets.CountTiles(fullRow).Should().Be(HomeWidgets.MaxPinned);
    }
}
