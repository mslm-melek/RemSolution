using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Branch.Commands.CreateBranchCommand;
using RemSolution.Application.Features.Branch.Commands.DeleteBranchCommand;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Branches.Commands;

using static Testing;

public class DeleteBranchTests : BaseTestFixture
{
    [Test]
    public async Task ShouldRequireValidBranchId()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() =>
            SendAsync(new DeleteBranchCommand(99))).Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ShouldDeleteBranch()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var country = new Country { Name = "Branchland" };
        await AddAsync(country);

        var branchId = await SendAsync(new CreateBranchCommand
        {
            Name = "Short lived",
            CountryId = country.Id
        });

        await SendAsync(new DeleteBranchCommand(branchId));

        (await FindAsync<Branch>(branchId)).Should().BeNull();
    }

    [Test]
    public async Task ShouldNotDeleteAnotherAgencysBranch()
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

        var agencyA = GetAgencyId();

        // Switch tenant: agency B cannot even see the branch, let alone delete it.
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() =>
            SendAsync(new DeleteBranchCommand(branchId))).Should().ThrowAsync<NotFoundException>();

        SetCurrentAgency(agencyA);
        (await FindAsync<Branch>(branchId)).Should().NotBeNull();
    }
}
