using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Client.Commands.CreateClientCommand;
using RemSolution.Application.Features.Client.Commands.DeleteClientCommand;
using RemSolution.Application.Features.Client.Queries.GetClientsWithPaginationQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Identity;

using static Testing;

public class PermissionAuthorizationTests : BaseTestFixture
{
    [Test]
    public async Task PermissionGatedCommandsShouldRequireAuthenticatedUser()
    {
        await FluentActions.Invoking(() =>
            SendAsync(new CreateClientCommand { FirstName = "Ghost", LastName = "User" }))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public async Task StaffWithoutPermissionShouldBeForbidden()
    {
        await RunAsAgencyStaffAsync(); // no grants at all
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() =>
            SendAsync(new CreateClientCommand
            {
                FirstName = "Not",
                LastName = "Allowed",
                BirthDate = new DateTime(1990, 5, 20)
            }))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task StaffWithPermissionShouldPass()
    {
        var userId = await RunAsAgencyStaffAsync(Permissions.ClientCreate, Permissions.ClientRead);
        var agencyId = await AddTestAgencyAsync();

        var clientId = await SendAsync(new CreateClientCommand
        {
            FirstName = "Granted",
            LastName = "Staff",
            BirthDate = new DateTime(1990, 5, 20)
        });

        var client = await FindAsync<Client>(clientId);
        client!.AgencyId.Should().Be(agencyId);
        client.CreatedBy.Should().Be(userId);

        var page = await SendAsync(new GetClientsWithPaginationQuery());
        page.TotalCount.Should().Be(1);
    }

    [Test]
    public async Task PermissionsAreNotInterchangeable()
    {
        // Client.Create does not imply Client.Delete: each action is its own
        // grant, that is the point of replacing the all-or-nothing staff role.
        await RunAsAgencyStaffAsync(Permissions.ClientCreate);
        await AddTestAgencyAsync();

        var clientId = await SendAsync(new CreateClientCommand
        {
            FirstName = "Kept",
            LastName = "Around",
            BirthDate = new DateTime(1990, 5, 20)
        });

        await FluentActions.Invoking(() =>
            SendAsync(new DeleteClientCommand(clientId)))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task AgencyAdministratorHoldsEveryPermissionImplicitly()
    {
        // No UserPermission rows exist for the admin; the role alone passes.
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var clientId = await SendAsync(new CreateClientCommand
        {
            FirstName = "Implicit",
            LastName = "Admin",
            BirthDate = new DateTime(1990, 5, 20)
        });

        await SendAsync(new DeleteClientCommand(clientId));

        (await FindAsync<Client>(clientId)).Should().BeNull();
    }
}
