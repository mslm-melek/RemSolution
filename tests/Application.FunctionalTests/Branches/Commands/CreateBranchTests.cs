using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Branch.Commands.CreateBranchCommand;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Branches.Commands;

using static Testing;

public class CreateBranchTests : BaseTestFixture
{
    [Test]
    public async Task ShouldRequireMinimumFields()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var command = new CreateBranchCommand(); // empty

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldRequireBothCoordinatesTogether()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var country = new Country { Name = "Branchland" };
        await AddAsync(country);

        var command = new CreateBranchCommand
        {
            Name = "Half located",
            CountryId = country.Id,
            Latitude = 36.8,
        };

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldBeForbiddenForAgencyStaff()
    {
        await RunAsDefaultUserAsync();
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() =>
            SendAsync(new CreateBranchCommand { Name = "Not allowed", CountryId = 1 }))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task ShouldCreateBranchStampedWithCurrentTenant()
    {
        var userId = await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        var country = new Country { Name = "Branchland" };
        await AddAsync(country);

        var branchId = await SendAsync(new CreateBranchCommand
        {
            Name = "Downtown",
            CountryId = country.Id,
            Latitude = 36.8065,
            Longitude = 10.1815
        });

        var branch = await FindAsync<Branch>(branchId);

        branch.Should().NotBeNull();
        branch!.Name.Should().Be("Downtown");
        branch.CountryId.Should().Be(country.Id);
        branch.AgencyId.Should().Be(agencyId);
        branch.CreatedBy.Should().Be(userId);
        branch.Location.Should().NotBeNull();
        branch.Location!.Y.Should().BeApproximately(36.8065, 1e-9);
        branch.Location.X.Should().BeApproximately(10.1815, 1e-9);
        branch.Location.SRID.Should().Be(4326);
    }

    [Test]
    public async Task ShouldAllowBranchWithoutLocationUntilGeocoded()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var country = new Country { Name = "Branchland" };
        await AddAsync(country);

        var branchId = await SendAsync(new CreateBranchCommand
        {
            Name = "Not geocoded yet",
            CountryId = country.Id
        });

        var branch = await FindAsync<Branch>(branchId);

        branch!.Location.Should().BeNull();
    }
}
