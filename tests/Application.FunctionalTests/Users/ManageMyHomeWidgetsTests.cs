using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Users.Commands.UpdateMyHomeWidgetsCommand;
using RemSolution.Domain.Constants;
using RemSolution.Infrastructure.Identity;

namespace RemSolution.Application.FunctionalTests.Users;

using static Testing;

public class ManageMyHomeWidgetsTests : BaseTestFixture
{
    [Test]
    public async Task ShouldStoreTheChoiceInTheOrderItWasMade()
    {
        var userId = await RunAsDefaultUserAsync();

        (await FindAsync<ApplicationUser>(userId))!.HomeWidgets
            .Should().BeNull("a new account has never chosen, and gets the default tiles");

        await SendAsync(new UpdateMyHomeWidgetsCommand
        {
            Widgets = new[] { HomeWidgets.Credits, HomeWidgets.Cars }
        });

        var stored = (await FindAsync<ApplicationUser>(userId))!.HomeWidgets;

        HomeWidgets.Parse(stored).Should().Equal(HomeWidgets.Credits, HomeWidgets.Cars);
    }

    [Test]
    public async Task ShouldKeepChoosingNothingApartFromNeverChoosing()
    {
        var userId = await RunAsDefaultUserAsync();

        await SendAsync(new UpdateMyHomeWidgetsCommand { Widgets = Array.Empty<string>() });

        var stored = (await FindAsync<ApplicationUser>(userId))!.HomeWidgets;

        // Stored empty, not null: the home screen must show no tiles rather than
        // falling back to the defaults the user just cleared.
        stored.Should().BeEmpty();
        HomeWidgets.Parse(stored).Should().BeEmpty();
    }

    [Test]
    public async Task ShouldRejectAnUnknownWidget()
    {
        await RunAsDefaultUserAsync();

        await FluentActions.Invoking(() =>
            SendAsync(new UpdateMyHomeWidgetsCommand { Widgets = new[] { "Antiques" } }))
            .Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldRejectTheSameWidgetTwice()
    {
        await RunAsDefaultUserAsync();

        await FluentActions.Invoking(() =>
            SendAsync(new UpdateMyHomeWidgetsCommand
            {
                Widgets = new[] { HomeWidgets.Cars, HomeWidgets.Cars }
            }))
            .Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldRejectMoreWidgetsThanMayBePinned()
    {
        await RunAsDefaultUserAsync();

        // Tiles: the cap is on the row, and the panel widgets are exempt from it.
        await FluentActions.Invoking(() =>
            SendAsync(new UpdateMyHomeWidgetsCommand
            {
                Widgets = HomeWidgets.All.Where(key => !HomeWidgets.IsPanel(key))
                    .Take(HomeWidgets.MaxPinned + 1)
                    .ToArray()
            }))
            .Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldAcceptAPanelOnTopOfAFullTileRow()
    {
        var userId = await RunAsDefaultUserAsync();

        var widgets = HomeWidgets.All.Where(key => !HomeWidgets.IsPanel(key))
            .Take(HomeWidgets.MaxPinned)
            .Append(HomeWidgets.Calendar)
            .ToArray();

        await SendAsync(new UpdateMyHomeWidgetsCommand { Widgets = widgets });

        var stored = (await FindAsync<ApplicationUser>(userId))!.HomeWidgets;

        HomeWidgets.Parse(stored).Should().Equal(widgets);
    }
}
