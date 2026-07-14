using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Branch.Commands.CreateBranchCommand;
using RemSolution.Application.Features.Branch.Queries.GetBranchesQuery;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Branches.Queries;

using static Testing;

public class GetBranchesQueryTests : BaseTestFixture
{
    [Test]
    public async Task ShouldBeForbiddenForAgencyStaff()
    {
        await RunAsDefaultUserAsync();
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() =>
            SendAsync(new GetBranchesQuery())).Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task ShouldReturnOnlyCurrentAgencysBranchesOrderedByName()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var country = new Country { Name = "Branchland" };
        await AddAsync(country);

        await SendAsync(new CreateBranchCommand { Name = "Zebra", CountryId = country.Id });
        await SendAsync(new CreateBranchCommand
        {
            Name = "Alpha",
            CountryId = country.Id,
            Latitude = 36.8065,
            Longitude = 10.1815
        });

        var result = await SendAsync(new GetBranchesQuery());

        result.Should().HaveCount(2);
        result.Select(b => b.Name).Should().ContainInOrder("Alpha", "Zebra");
        result[0].CountryName.Should().Be("Branchland");
        result[0].Latitude.Should().BeApproximately(36.8065, 1e-9);
        result[0].Longitude.Should().BeApproximately(10.1815, 1e-9);

        // Switch tenant: agency B sees an empty list, not agency A's branches.
        await AddTestAgencyAsync();

        (await SendAsync(new GetBranchesQuery())).Should().BeEmpty();
    }
}
