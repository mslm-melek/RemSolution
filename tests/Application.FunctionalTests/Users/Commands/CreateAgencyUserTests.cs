using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Users.Commands.CreateAgencyUserCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Infrastructure.Identity;

namespace RemSolution.Application.FunctionalTests.Users.Commands;

using static Testing;

public class CreateAgencyUserTests : BaseTestFixture
{
    private static CreateAgencyUserCommand SomeStaff(string userName = "staff1@agency.local") => new()
    {
        UserName = userName,
        Password = "Staff1234!",
        Permissions = new[] { Permissions.ClientRead, Permissions.ClientCreate }
    };

    [Test]
    public async Task ShouldCreateStaffUserInCurrentAgencyWithGrants()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        var userId = await SendAsync(SomeStaff());

        var user = await FindAsync<ApplicationUser>(userId);

        user.Should().NotBeNull();
        user!.UserName.Should().Be("staff1@agency.local");
        user.AgencyId.Should().Be(agencyId);

        (await IsInRoleAsync(userId, Roles.AgencyStaff)).Should().BeTrue();

        (await CountAsync<UserPermission>()).Should().Be(2);
    }

    [Test]
    public async Task ShouldEnforceMaxUsersUnderThePlan()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync(maxUsers: 1);

        await SendAsync(SomeStaff("first@agency.local"));

        await FluentActions.Invoking(() =>
            SendAsync(SomeStaff("second@agency.local")))
            .Should().ThrowAsync<PlanLimitExceededException>();

        // The refused create must leave nothing behind: no user, no grants.
        (await CountAsync<UserPermission>()).Should().Be(2);
    }

    [Test]
    public async Task ShouldRequireActiveSubscription()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync(withSubscription: false);

        await FluentActions.Invoking(() =>
            SendAsync(SomeStaff()))
            .Should().ThrowAsync<SubscriptionRequiredException>();
    }

    [Test]
    public async Task ShouldBeForbiddenForAgencyStaff()
    {
        await RunAsAgencyStaffAsync(Permissions.ClientRead);
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() =>
            SendAsync(SomeStaff()))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task ShouldRejectUnknownPermissionNames()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var command = SomeStaff() with { Permissions = new[] { "Client.Fly" } };

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldSurfaceIdentityErrorsAsValidationFailures()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await SendAsync(SomeStaff("dup@agency.local"));

        // Same user name again: Identity refuses; the API answers 400, not 500.
        await FluentActions.Invoking(() =>
            SendAsync(SomeStaff("dup@agency.local")))
            .Should().ThrowAsync<ValidationException>();
    }
}
