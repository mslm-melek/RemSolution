using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Branch.Commands.CreateBranchCommand;
using RemSolution.Application.Features.Branch.Queries.GetBranchByIdQuery;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Branches.Queries;

using static Testing;

public class GetBranchByIdQueryTests : BaseTestFixture
{
    [Test]
    public async Task ShouldThrowNotFoundForUnknownId()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() =>
            SendAsync(new GetBranchByIdQuery(99))).Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ShouldReturnBranchWithCoordinates()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var country = new Country { Name = "Branchland" };
        await AddAsync(country);

        var branchId = await SendAsync(new CreateBranchCommand
        {
            Name = "Downtown",
            CountryId = country.Id,
            Latitude = 36.8065,
            Longitude = 10.1815
        });

        var result = await SendAsync(new GetBranchByIdQuery(branchId));

        result.Id.Should().Be(branchId);
        result.Name.Should().Be("Downtown");
        result.CountryId.Should().Be(country.Id);
        result.CountryName.Should().Be("Branchland");
        result.Latitude.Should().BeApproximately(36.8065, 1e-9);
        result.Longitude.Should().BeApproximately(10.1815, 1e-9);
    }

    [Test]
    public async Task ShouldNotReturnAnotherAgencysBranch()
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

        // Switch tenant: the branch belongs to agency A and must 404 for B.
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() =>
            SendAsync(new GetBranchByIdQuery(branchId))).Should().ThrowAsync<NotFoundException>();
    }
}
