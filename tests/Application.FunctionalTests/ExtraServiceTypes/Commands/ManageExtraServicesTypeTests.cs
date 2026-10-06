using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.ExtraServicesType.Commands.CreateExtraServicesTypeCommand;
using RemSolution.Application.Features.ExtraServicesTypeTemplate.Commands.CreateExtraServicesTypeTemplateCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.ExtraServiceTypes.Commands;

using static Testing;

public class ManageExtraServicesTypeTests : BaseTestFixture
{
    [Test]
    public async Task RegularStaffCannotManageTheCatalog()
    {
        // Staff even holding the operational ExtraService permission may not
        // manage the agency's type catalog.
        await RunAsAgencyStaffAsync(Permissions.ExtraServiceCreate);
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() =>
            SendAsync(new CreateExtraServicesTypeCommand { Name = "GPS", Amount = 10m }))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task AgencyAdministratorCanManageTheCatalog()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var id = await SendAsync(new CreateExtraServicesTypeCommand { Name = "GPS", Amount = 10m });

        (await FindAsync<ExtraServicesType>(id))!.Name.Should().Be("GPS");
    }

    [Test]
    public async Task AgencyAdministratorCannotManageWhenFeatureIsOff()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        // The agency has not activated ExtraServices.
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.ExtraServices, Enabled = false });

        await FluentActions.Invoking(() =>
            SendAsync(new CreateExtraServicesTypeCommand { Name = "GPS", Amount = 10m }))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task PlatformAdministratorManagesTheStandardTypesAsTemplates()
    {
        // The app admin has no agency: what it offers everyone is a template,
        // and a template has no price because it has no currency.
        await RunAsPlatformAdministratorAsync();

        var id = await SendAsync(new CreateExtraServicesTypeTemplateCommand { Name = "Child seat" });

        (await FindAsync<ExtraServicesTypeTemplate>(id))!.Name.Should().Be("Child seat");
    }
}
