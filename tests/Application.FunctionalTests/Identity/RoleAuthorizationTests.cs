using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Agency.Commands.CreateAgencyCommand;
using RemSolution.Application.Features.SubscriptionPlan.Queries.GetSubscriptionPlansQuery;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Identity;

using static Testing;

public class RoleAuthorizationTests : BaseTestFixture
{
    [Test]
    public async Task PlatformCommandsShouldRequireAuthenticatedUser()
    {
        await FluentActions.Invoking(() =>
            SendAsync(new CreateAgencyCommand { Name = "Ghost", CountryId = 1 }))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public async Task PlatformCommandsShouldBeForbiddenForAgencyUsers()
    {
        await RunAsDefaultUserAsync();

        await FluentActions.Invoking(() =>
            SendAsync(new CreateAgencyCommand { Name = "Not allowed", CountryId = 1 }))
            .Should().ThrowAsync<ForbiddenAccessException>();

        await FluentActions.Invoking(() =>
            SendAsync(new GetSubscriptionPlansQuery()))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task PlatformAdministratorShouldManageAgencies()
    {
        await RunAsPlatformAdministratorAsync();

        var country = new Country { Name = "Adminland" };
        await AddAsync(country);

        var created = await SendAsync(new CreateAgencyCommand
        {
            Name = "Created by platform admin",
            CountryId = country.Id,
            AdminEmail = "admin@created-by-platform-admin.test"
        });

        var agency = await FindAsync<Agency>(created.Id);

        agency.Should().NotBeNull();
        agency!.Name.Should().Be("Created by platform admin");
    }
}
