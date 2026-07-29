using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Agency.Commands.CreateAgencyBranchCommand;
using RemSolution.Application.Features.Agency.Commands.CreateAgencyCommand;
using RemSolution.Application.Features.Agency.Commands.DeleteAgencyBranchCommand;
using RemSolution.Application.Features.Agency.Commands.UpdateAgencyBranchCommand;
using RemSolution.Application.Features.Agency.Queries.GetAgencyBranchesQuery;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Agencies.Commands;

using static Testing;

// The platform-administrator branch sub-resource, used while editing an agency.
// The caller belongs to no agency, so the agency under test is created through
// CreateAgencyCommand rather than AddTestAgencyAsync — which would make it the
// ambient tenant and stop these tests proving anything about scoping.
public class AgencyBranchTests : BaseTestFixture
{
    private async Task<(int agencyId, int countryId)> AnAgencyAsync(string name = "Sud Cars")
    {
        var country = new Country { Name = $"Agencyland for {name}" };
        await AddAsync(country);

        var agencyId = await SendAsync(new CreateAgencyCommand
        {
            Name = name,
            CountryId = country.Id
        });

        return (agencyId, country.Id);
    }

    [Test]
    public async Task ShouldCreateReadUpdateAndDeleteABranch()
    {
        await RunAsPlatformAdministratorAsync();
        var (agencyId, countryId) = await AnAgencyAsync();

        var branchId = await SendAsync(new CreateAgencyBranchCommand
        {
            AgencyId = agencyId,
            Name = "Downtown",
            CountryId = countryId,
            Address = "Rue de la République",
            Latitude = 34.7398,
            Longitude = 10.7601
        });

        var created = await FindIgnoringFiltersAsync<Branch>(b => b.Id == branchId);
        created!.AgencyId.Should().Be(agencyId);
        created.Address.Should().Be("Rue de la République");
        created.Location!.Y.Should().BeApproximately(34.7398, 1e-9);

        var listed = await SendAsync(new GetAgencyBranchesQuery(agencyId));
        listed.Should().HaveCount(1);
        listed[0].Name.Should().Be("Downtown");
        listed[0].Address.Should().Be("Rue de la République");
        listed[0].Latitude.Should().BeApproximately(34.7398, 1e-9);
        listed[0].Longitude.Should().BeApproximately(10.7601, 1e-9);

        await SendAsync(new UpdateAgencyBranchCommand
        {
            AgencyId = agencyId,
            Id = branchId,
            Name = "Airport",
            CountryId = countryId,
            Address = "Sfax-Thyna Airport",
            Latitude = 34.7178,
            Longitude = 10.6908
        });

        var updated = await FindIgnoringFiltersAsync<Branch>(b => b.Id == branchId);
        updated!.Name.Should().Be("Airport");
        updated.Address.Should().Be("Sfax-Thyna Airport");
        updated.Location!.X.Should().BeApproximately(10.6908, 1e-9);
        // Still the same agency: AgencyId is never taken from the request.
        updated.AgencyId.Should().Be(agencyId);

        await SendAsync(new DeleteAgencyBranchCommand(agencyId, branchId));

        (await FindIgnoringFiltersAsync<Branch>(b => b.Id == branchId)).Should().BeNull();
    }

    [Test]
    public async Task ShouldOnlyListTheAgencysOwnBranches()
    {
        await RunAsPlatformAdministratorAsync();

        var (firstId, firstCountryId) = await AnAgencyAsync("First agency");
        var (secondId, secondCountryId) = await AnAgencyAsync("Second agency");

        await SendAsync(new CreateAgencyBranchCommand
        {
            AgencyId = firstId,
            Name = "Belongs to the first",
            CountryId = firstCountryId
        });
        await SendAsync(new CreateAgencyBranchCommand
        {
            AgencyId = secondId,
            Name = "Belongs to the second",
            CountryId = secondCountryId
        });

        var first = await SendAsync(new GetAgencyBranchesQuery(firstId));
        var second = await SendAsync(new GetAgencyBranchesQuery(secondId));

        first.Should().ContainSingle(b => b.Name == "Belongs to the first");
        second.Should().ContainSingle(b => b.Name == "Belongs to the second");
    }

    // The tenant filter, not an id check, is what isolates these: a branch of
    // another agency is simply not there to be found.
    [Test]
    public async Task ShouldNotUpdateABranchOfAnotherAgency()
    {
        await RunAsPlatformAdministratorAsync();

        var (firstId, firstCountryId) = await AnAgencyAsync("First agency");
        var (secondId, _) = await AnAgencyAsync("Second agency");

        var branchId = await SendAsync(new CreateAgencyBranchCommand
        {
            AgencyId = firstId,
            Name = "First agency branch",
            CountryId = firstCountryId
        });

        await FluentActions.Invoking(() => SendAsync(new UpdateAgencyBranchCommand
        {
            AgencyId = secondId,
            Id = branchId,
            Name = "Hijacked",
            CountryId = firstCountryId
        })).Should().ThrowAsync<NotFoundException>();

        (await FindIgnoringFiltersAsync<Branch>(b => b.Id == branchId))!.Name.Should().Be("First agency branch");
    }

    [Test]
    public async Task ShouldNotDeleteABranchOfAnotherAgency()
    {
        await RunAsPlatformAdministratorAsync();

        var (firstId, firstCountryId) = await AnAgencyAsync("First agency");
        var (secondId, _) = await AnAgencyAsync("Second agency");

        var branchId = await SendAsync(new CreateAgencyBranchCommand
        {
            AgencyId = firstId,
            Name = "First agency branch",
            CountryId = firstCountryId
        });

        await FluentActions.Invoking(() =>
            SendAsync(new DeleteAgencyBranchCommand(secondId, branchId)))
            .Should().ThrowAsync<NotFoundException>();

        (await FindIgnoringFiltersAsync<Branch>(b => b.Id == branchId)).Should().NotBeNull();
    }

    // Subscription enforcement blocks an agency from changing its own data once
    // it stops paying; it must not block the app owner from administering it. A
    // freshly created agency has no subscription at all (one is assigned after it
    // exists), which is the same state a lapsed one is in as far as the write
    // interceptor is concerned.
    [Test]
    public async Task ShouldManageBranchesForAnAgencyWithNoSubscription()
    {
        await RunAsPlatformAdministratorAsync();
        var (agencyId, countryId) = await AnAgencyAsync();

        (await AllAsync<AgencySubscription>()).Should().BeEmpty();

        var branchId = await SendAsync(new CreateAgencyBranchCommand
        {
            AgencyId = agencyId,
            Name = "Set up before billing",
            CountryId = countryId
        });

        await SendAsync(new UpdateAgencyBranchCommand
        {
            AgencyId = agencyId,
            Id = branchId,
            Name = "Renamed before billing",
            CountryId = countryId
        });

        (await FindIgnoringFiltersAsync<Branch>(b => b.Id == branchId))!.Name.Should().Be("Renamed before billing");

        await SendAsync(new DeleteAgencyBranchCommand(agencyId, branchId));

        (await FindIgnoringFiltersAsync<Branch>(b => b.Id == branchId)).Should().BeNull();
    }

    [Test]
    public async Task ShouldRejectABranchForAnAgencyThatDoesNotExist()
    {
        await RunAsPlatformAdministratorAsync();

        var country = new Country { Name = "Agencyland" };
        await AddAsync(country);

        await FluentActions.Invoking(() => SendAsync(new CreateAgencyBranchCommand
        {
            AgencyId = 9999,
            Name = "Orphan",
            CountryId = country.Id
        })).Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ShouldBeForbiddenForAnAgencyAdministrator()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        await FluentActions.Invoking(() => SendAsync(new CreateAgencyBranchCommand
        {
            AgencyId = agencyId,
            Name = "Wrong door",
            CountryId = 1
        })).Should().ThrowAsync<ForbiddenAccessException>();

        await FluentActions.Invoking(() =>
            SendAsync(new GetAgencyBranchesQuery(agencyId)))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    // Removing a branch leaves the cars that were based there; Car.BranchId is
    // SetNull, so they are declassified rather than deleted or blocking.
    [Test]
    public async Task ShouldLeaveCarsBehindWhenTheirBranchIsDeleted()
    {
        await RunAsPlatformAdministratorAsync();
        var (agencyId, countryId) = await AnAgencyAsync();

        var branchId = await SendAsync(new CreateAgencyBranchCommand
        {
            AgencyId = agencyId,
            Name = "Downtown",
            CountryId = countryId
        });

        var brand = new Brand { Name = "Testmobile" };
        await AddAsync(brand);
        var model = new ModelCar { Name = "Basic", BrandId = brand.Id };
        await AddAsync(model);

        var car = new Car
        {
            Matricule = "123 TN 4567",
            ModelId = model.Id,
            BranchId = branchId,
            AgencyId = agencyId
        };
        await AddAsync(car);

        await SendAsync(new DeleteAgencyBranchCommand(agencyId, branchId));

        var survivor = await FindIgnoringFiltersAsync<Car>(c => c.Id == car.Id);

        survivor.Should().NotBeNull();
        survivor!.BranchId.Should().BeNull();
    }
}
