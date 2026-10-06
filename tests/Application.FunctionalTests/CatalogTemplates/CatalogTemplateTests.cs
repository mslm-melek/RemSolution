using System.Globalization;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Agency.Commands.CreateAgencyCommand;
using RemSolution.Application.Features.ExpenseType.Commands.CreateExpenseTypeCommand;
using RemSolution.Application.Features.ExpenseType.Commands.ResetExpenseTypeCommand;
using RemSolution.Application.Features.ExpenseType.Commands.UpdateExpenseTypeCommand;
using RemSolution.Application.Features.ExpenseType.Queries.GetExpenseTypesQuery;
using RemSolution.Application.Features.ExpenseTypeTemplate.Commands.CreateExpenseTypeTemplateCommand;
using RemSolution.Application.Features.ExpenseTypeTemplate.Commands.DeactivateExpenseTypeTemplateCommand;
using RemSolution.Application.Features.ExpenseTypeTemplate.Commands.UpdateExpenseTypeTemplateCommand;
using RemSolution.Application.Features.ExtraServicesType.Commands.CreateExtraServicesTypeCommand;
using RemSolution.Application.Features.ExtraServicesType.Commands.ResetExtraServicesTypeCommand;
using RemSolution.Application.Features.ExtraServicesType.Commands.UpdateExtraServicesTypeCommand;
using RemSolution.Application.Features.ExtraServicesType.Queries.GetExtraServicesTypesQuery;
using RemSolution.Application.Features.ExtraServicesTypeTemplate.Commands.CreateExtraServicesTypeTemplateCommand;
using RemSolution.Application.Features.ExtraServicesTypeTemplate.Commands.UpdateExtraServicesTypeTemplateCommand;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.CatalogTemplates;

using static Testing;

// The platform's standard expense and add-on types are templates; every agency
// works on its own copy, and adds types nobody else sees.
public class CatalogTemplateTests : BaseTestFixture
{
    [Test]
    public async Task ANewTemplateReachesEveryExistingAgency()
    {
        var agencyA = await AddTestAgencyAsync();
        var agencyB = await AddTestAgencyAsync();

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);

        var templateId = await SendAsync(new CreateExpenseTypeTemplateCommand
        {
            Name = "oilChange", AfterKilometer = 10_000
        });

        var copies = (await AllIgnoringFiltersAsync<ExpenseType>())
            .Where(t => t.TemplateId == templateId)
            .ToList();

        copies.Select(c => c.AgencyId).Should().BeEquivalentTo(new[] { agencyA, agencyB });
        copies.Should().AllSatisfy(c =>
        {
            c.Name.Should().Be("oilChange");
            c.AfterKilometer.Should().Be(10_000);
            c.IsActive.Should().BeTrue();
            c.IsCustomized.Should().BeFalse();
        });
    }

    [Test]
    public async Task ATemplateEditReachesOnlyTheCopiesAnAgencyLeftAlone()
    {
        var agencyA = await AddTestAgencyAsync();
        var agencyB = await AddTestAgencyAsync();

        var platformAdmin = await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);
        var templateId = await SendAsync(new CreateExpenseTypeTemplateCommand { Name = "Assurance", AfterMonth = 12 });

        // Agency A retunes its copy; agency B only switches its own off.
        await RunAsAgencyAdministratorAsync();
        SetCurrentAgency(agencyA);
        var copyA = (await SendAsync(new GetExpenseTypesQuery())).Single();
        await SendAsync(new UpdateExpenseTypeCommand
        {
            Id = copyA.Id, Name = "Assurance", AfterMonth = 6, IsActive = true
        });

        SetCurrentAgency(agencyB);
        var copyB = (await SendAsync(new GetExpenseTypesQuery())).Single();
        await SendAsync(new UpdateExpenseTypeCommand
        {
            Id = copyB.Id, Name = "Assurance", AfterMonth = 12, IsActive = false
        });

        SetCurrentUser(platformAdmin);
        SetCurrentAgency(null);
        await SendAsync(new UpdateExpenseTypeTemplateCommand
        {
            Id = templateId, Name = "Assurance auto", AfterMonth = 3, IsActive = true
        });

        var a = await FindIgnoringFiltersAsync<ExpenseType>(t => t.Id == copyA.Id);
        a!.IsCustomized.Should().BeTrue();
        a.Name.Should().Be("Assurance");
        a.AfterMonth.Should().Be(6);

        var b = await FindIgnoringFiltersAsync<ExpenseType>(t => t.Id == copyB.Id);
        b!.IsCustomized.Should().BeFalse();
        b.Name.Should().Be("Assurance auto");
        b.AfterMonth.Should().Be(3);
        // Switched off by the agency, and the platform does not switch it back on.
        b.IsActive.Should().BeFalse();
    }

    [Test]
    public async Task ARetiredTemplateLeavesExistingCopiesAndSkipsNewAgencies()
    {
        var existing = await AddTestAgencyAsync();

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);
        var kept = await SendAsync(new CreateExpenseTypeTemplateCommand { Name = "Vignette" });
        var retired = await SendAsync(new CreateExpenseTypeTemplateCommand { Name = "Fax" });
        await SendAsync(new DeactivateExpenseTypeTemplateCommand(retired));

        var country = new Country { Name = "Templateland" };
        await AddAsync(country);
        var created = await SendAsync(new CreateAgencyCommand
        {
            Name = "New Cars", CountryId = country.Id, AdminEmail = "boss@newcars.test"
        });

        var copies = await AllIgnoringFiltersAsync<ExpenseType>();

        copies.Where(c => c.AgencyId == created.Id).Select(c => c.TemplateId)
            .Should().BeEquivalentTo(new int?[] { kept });
        copies.Where(c => c.AgencyId == existing).Select(c => c.TemplateId)
            .Should().BeEquivalentTo(new int?[] { kept, retired });
    }

    [Test]
    public async Task AnAgencysOwnTypeIsInvisibleToAnotherAgency()
    {
        var agencyA = await AddTestAgencyAsync();
        var agencyB = await AddTestAgencyAsync();

        await RunAsAgencyAdministratorAsync();
        SetCurrentAgency(agencyA);
        var ownId = await SendAsync(new CreateExpenseTypeCommand { Name = "Parking" });

        (await SendAsync(new GetExpenseTypesQuery())).Select(t => t.Id).Should().Equal(ownId);

        SetCurrentAgency(agencyB);
        (await SendAsync(new GetExpenseTypesQuery())).Should().BeEmpty();

        await FluentActions.Invoking(() =>
            SendAsync(new UpdateExpenseTypeCommand { Id = ownId, Name = "Hijacked", IsActive = true }))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ThePlatformAdministratorOutsideAWorkspaceCannotAddAnAgencyType()
    {
        await RunAsPlatformAdministratorAsync();

        await FluentActions.Invoking(() =>
            SendAsync(new CreateExpenseTypeCommand { Name = "Orphan" }))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task AnAgencyAdministratorCannotManageTemplates()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() =>
            SendAsync(new CreateExpenseTypeTemplateCommand { Name = "Sneaky" }))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task AStandardNameIsServedAsItsKeyInEveryLanguage()
    {
        // The reader translates it (Transloco's catalogItems.*), so the API never
        // picks a language for it.
        var agencyId = await AddTestAgencyAsync();

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);
        await SendAsync(new CreateExtraServicesTypeTemplateCommand { Name = "babySeat" });

        await RunAsAgencyAdministratorAsync();
        SetCurrentAgency(agencyId);

        var previous = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("ar");
            var copy = (await SendAsync(new GetExtraServicesTypesQuery())).Single();
            copy.Name.Should().Be("babySeat");
            copy.IsStandard.Should().BeTrue();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
    [Test]
    public async Task AnAddOnPriceIsInTheAgencysCurrencyAndDoesNotStopRenames()
    {
        var agencyId = await AddTestAgencyAsync();

        var platformAdmin = await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);
        var templateId = await SendAsync(new CreateExtraServicesTypeTemplateCommand { Name = "GPS" });

        await RunAsAgencyAdministratorAsync();
        SetCurrentAgency(agencyId);
        var copy = (await SendAsync(new GetExtraServicesTypesQuery())).Single();
        copy.Amount.Should().BeNull();

        await SendAsync(new UpdateExtraServicesTypeCommand
        {
            Id = copy.Id, Name = "GPS", Amount = 25m, IsActive = true
        });

        var priced = (await SendAsync(new GetExtraServicesTypesQuery())).Single();
        priced.Amount!.Amount.Should().Be(25m);
        priced.Amount.Currency.Should().Be("TND");
        priced.IsCustomized.Should().BeFalse();

        SetCurrentUser(platformAdmin);
        SetCurrentAgency(null);
        await SendAsync(new UpdateExtraServicesTypeTemplateCommand
        {
            Id = templateId, Name = "Navigateur GPS", IsActive = true
        });

        var renamed = await FindIgnoringFiltersAsync<ExtraServicesType>(t => t.Id == copy.Id);
        renamed!.Name.Should().Be("Navigateur GPS");
        renamed.Amount!.Amount.Should().Be(25m);
    }

    [Test]
    public async Task ResettingACopyBringsBackTheStandardAndItsUpdates()
    {
        var agencyId = await AddTestAgencyAsync();

        var platformAdmin = await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);
        var templateId = await SendAsync(new CreateExpenseTypeTemplateCommand { Name = "Pneus", AfterKilometer = 40_000 });

        var agencyAdmin = await RunAsAgencyAdministratorAsync();
        SetCurrentAgency(agencyId);
        var copy = (await SendAsync(new GetExpenseTypesQuery())).Single();
        await SendAsync(new UpdateExpenseTypeCommand
        {
            Id = copy.Id, Name = "Pneumatiques", AfterKilometer = 30_000, IsActive = false
        });

        await SendAsync(new ResetExpenseTypeCommand(copy.Id));

        var reset = (await SendAsync(new GetExpenseTypesQuery())).Single();
        reset.Name.Should().Be("Pneus");
        reset.AfterKilometer.Should().Be(40_000);
        reset.IsCustomized.Should().BeFalse();
        // Switched off by the agency, and the reset leaves that alone.
        reset.IsActive.Should().BeFalse();

        SetCurrentUser(platformAdmin);
        SetCurrentAgency(null);
        await SendAsync(new UpdateExpenseTypeTemplateCommand
        {
            Id = templateId, Name = "Pneus", AfterKilometer = 50_000, IsActive = true
        });

        SetCurrentUser(agencyAdmin);
        SetCurrentAgency(agencyId);
        (await SendAsync(new GetExpenseTypesQuery())).Single().AfterKilometer.Should().Be(50_000);
    }

    [Test]
    public async Task ResettingAnAddOnKeepsItsPrice()
    {
        var agencyId = await AddTestAgencyAsync();

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);
        await SendAsync(new CreateExtraServicesTypeTemplateCommand { Name = "GPS" });

        await RunAsAgencyAdministratorAsync();
        SetCurrentAgency(agencyId);
        var copy = (await SendAsync(new GetExtraServicesTypesQuery())).Single();
        await SendAsync(new UpdateExtraServicesTypeCommand
        {
            Id = copy.Id, Name = "Navigation", Amount = 20m, IsActive = true
        });

        await SendAsync(new ResetExtraServicesTypeCommand(copy.Id));

        var reset = (await SendAsync(new GetExtraServicesTypesQuery())).Single();
        reset.Name.Should().Be("GPS");
        reset.IsCustomized.Should().BeFalse();
        reset.Amount!.Amount.Should().Be(20m);
    }

    [Test]
    public async Task AnAgencysOwnTypeHasNothingToResetTo()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var id = await SendAsync(new CreateExpenseTypeCommand { Name = "Parking" });

        await FluentActions.Invoking(() => SendAsync(new ResetExpenseTypeCommand(id)))
            .Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task AnAgencyCanAddItsOwnPricedAddOn()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var id = await SendAsync(new CreateExtraServicesTypeCommand { Name = "Delivery", Amount = 30m });

        var type = await FindAsync<ExtraServicesType>(id);
        type!.TemplateId.Should().BeNull();
        type.Amount!.Currency.Should().Be("TND");
    }
}
