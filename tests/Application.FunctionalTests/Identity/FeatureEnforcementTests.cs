using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Client.Commands.CreateClientCommand;
using RemSolution.Application.Features.Client.Queries.GetClientsWithPaginationQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Identity;

using static Testing;

public class FeatureEnforcementTests : BaseTestFixture
{
    private static CreateClientCommand SomeClient => new()
    {
        FirstName = "John",
        LastName = "Doe",
        BirthDate = new DateTime(1990, 5, 20)
    };

    [Test]
    public async Task FeatureIsEnabledByDefaultWithoutAnyRow()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var clientId = await SendAsync(SomeClient);

        (await FindAsync<Client>(clientId)).Should().NotBeNull();
    }

    [Test]
    public async Task DisabledFeatureShouldForbidTheWholeModuleEvenForTheAdministrator()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Clients, Enabled = false });

        await FluentActions.Invoking(() =>
            SendAsync(SomeClient)).Should().ThrowAsync<ForbiddenAccessException>();

        await FluentActions.Invoking(() =>
            SendAsync(new GetClientsWithPaginationQuery()))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task ExplicitlyEnabledRowShouldAllowTheModule()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Clients, Enabled = true });

        var clientId = await SendAsync(SomeClient);

        (await FindAsync<Client>(clientId)).Should().NotBeNull();
    }

    [Test]
    public async Task DisablingAFeatureShouldNotAffectOtherModules()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Cars, Enabled = false });

        var clientId = await SendAsync(SomeClient);

        (await FindAsync<Client>(clientId)).Should().NotBeNull();
    }

    [Test]
    public async Task DisablingAFeatureShouldNotAffectOtherAgencies()
    {
        await RunAsAgencyAdministratorAsync();

        // Agency A switches Clients off.
        await AddTestAgencyAsync();
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Clients, Enabled = false });

        // Agency B is untouched: the toggle is tenant data like any other.
        await AddTestAgencyAsync();

        var clientId = await SendAsync(SomeClient);

        (await FindAsync<Client>(clientId)).Should().NotBeNull();
    }
}
