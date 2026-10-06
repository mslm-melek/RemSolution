using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.ExpenseType.Commands.CreateExpenseTypeCommand;
using RemSolution.Application.Features.ExpenseTypeTemplate.Commands.CreateExpenseTypeTemplateCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.ExpenseTypes.Commands;

using static Testing;

public class ManageExpenseTypeTests : BaseTestFixture
{
    [Test]
    public async Task RegularStaffCannotManageTheCatalog()
    {
        // Staff even holding the operational Expense permission may not manage
        // the agency's type catalog.
        await RunAsAgencyStaffAsync(Permissions.ExpenseCreate);
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() =>
            SendAsync(new CreateExpenseTypeCommand { Name = "Oil change", AfterKilometer = 10000 }))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task AgencyAdministratorCanManageTheCatalog()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var id = await SendAsync(new CreateExpenseTypeCommand { Name = "Oil change", AfterKilometer = 10000 });

        var type = await FindAsync<ExpenseType>(id);
        type!.Name.Should().Be("Oil change");
        type.AgencyId.Should().Be(GetAgencyId());
        type.TemplateId.Should().BeNull();
    }

    [Test]
    public async Task AgencyAdministratorCannotManageWhenFeatureIsOff()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        // The agency has not activated Expenses.
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Expenses, Enabled = false });

        await FluentActions.Invoking(() =>
            SendAsync(new CreateExpenseTypeCommand { Name = "Oil change" }))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task PlatformAdministratorManagesTheStandardTypesAsTemplates()
    {
        // The app admin has no agency: what it offers everyone is a template.
        await RunAsPlatformAdministratorAsync();

        var id = await SendAsync(new CreateExpenseTypeTemplateCommand { Name = "Insurance", AfterMonth = 12 });

        (await FindAsync<ExpenseTypeTemplate>(id))!.Name.Should().Be("Insurance");
    }
}
