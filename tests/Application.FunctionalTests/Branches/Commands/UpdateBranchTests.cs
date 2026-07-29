using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Branch.Commands.CreateBranchCommand;
using RemSolution.Application.Features.Branch.Commands.UpdateBranchCommand;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Branches.Commands;

using static Testing;

public class UpdateBranchTests : BaseTestFixture
{
    [Test]
    public async Task ShouldRequireValidBranchId()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var command = new UpdateBranchCommand { Id = 99, Name = "Ghost", CountryId = 1 };

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ShouldUpdateBranch()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var country = new Country { Name = "Branchland" };
        await AddAsync(country);

        var branchId = await SendAsync(new CreateBranchCommand
        {
            Name = "Downtown",
            CountryId = country.Id,
            Address = "12 Avenue Habib Bourguiba, Tunis",
            Latitude = 36.8065,
            Longitude = 10.1815
        });

        await SendAsync(new UpdateBranchCommand
        {
            Id = branchId,
            Name = "Airport",
            CountryId = country.Id,
            Address = "Tunis-Carthage Airport",
            Latitude = 36.851,
            Longitude = 10.2272
        });

        var branch = await FindAsync<Branch>(branchId);

        branch!.Name.Should().Be("Airport");
        branch.Address.Should().Be("Tunis-Carthage Airport");
        branch.Location!.Y.Should().BeApproximately(36.851, 1e-9);
        branch.Location.X.Should().BeApproximately(10.2272, 1e-9);
    }

    // Moving the pin away is a legitimate edit, not a no-op: the address stays as
    // typed while the coordinates clear.
    [Test]
    public async Task ShouldClearTheLocationWhenCoordinatesAreOmitted()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var country = new Country { Name = "Branchland" };
        await AddAsync(country);

        var branchId = await SendAsync(new CreateBranchCommand
        {
            Name = "Downtown",
            CountryId = country.Id,
            Address = "Somewhere in Tunis",
            Latitude = 36.8065,
            Longitude = 10.1815
        });

        await SendAsync(new UpdateBranchCommand
        {
            Id = branchId,
            Name = "Downtown",
            CountryId = country.Id,
            Address = "Somewhere in Tunis"
        });

        var branch = await FindAsync<Branch>(branchId);

        branch!.Location.Should().BeNull();
        branch.Address.Should().Be("Somewhere in Tunis");
    }

    [Test]
    public async Task ShouldNotUpdateAnotherAgencysBranch()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var country = new Country { Name = "Branchland" };
        await AddAsync(country);

        var branchId = await SendAsync(new CreateBranchCommand
        {
            Name = "Agency A branch",
            CountryId = country.Id
        });

        // Switch tenant: the branch must be invisible to agency B.
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() =>
            SendAsync(new UpdateBranchCommand { Id = branchId, Name = "Hijacked", CountryId = country.Id }))
            .Should().ThrowAsync<NotFoundException>();
    }
}
