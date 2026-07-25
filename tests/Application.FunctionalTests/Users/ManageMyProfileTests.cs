using RemSolution.Application.Features.Users.Commands.UpdateMyProfileCommand;
using RemSolution.Application.Features.Users.Queries.GetMyProfileQuery;

namespace RemSolution.Application.FunctionalTests.Users;

using static Testing;

public class ManageMyProfileTests : BaseTestFixture
{
    [Test]
    public async Task ShouldReturnAndUpdateMyOwnProfile()
    {
        await RunAsDefaultUserAsync(); // created as "test@local"

        var before = await SendAsync(new GetMyProfileQuery());
        before.UserName.Should().Be("test@local");
        before.Email.Should().Be("test@local");

        // Same email → display-name-only change (no login change).
        await SendAsync(new UpdateMyProfileCommand { FullName = "New Name", Email = "test@local" });

        var after = await SendAsync(new GetMyProfileQuery());
        after.FullName.Should().Be("New Name");
    }

    [Test]
    public async Task ShouldRejectAnInvalidEmail()
    {
        await RunAsDefaultUserAsync();

        await FluentActions.Invoking(() =>
            SendAsync(new UpdateMyProfileCommand { FullName = "X", Email = "not-an-email" }))
            .Should().ThrowAsync<RemSolution.Application.Common.Exceptions.ValidationException>();
    }
}
