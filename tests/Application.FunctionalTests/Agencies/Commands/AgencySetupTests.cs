using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Agency.Commands.CreateAgencyCarCommand;
using RemSolution.Application.Features.Agency.Commands.SetAgencyInvoiceSettingsCommand;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.FunctionalTests.Agencies.Commands;

using static Testing;

/// <summary>
/// The two steps of the opening wizard the agency cannot take for itself,
/// because at that point nobody has signed in to it: its invoice settings and
/// its first cars.
/// </summary>
public class AgencySetupTests : BaseTestFixture
{
    [Test]
    public async Task ThePlatformCanPutTheFirstCarIntoAnAgency()
    {
        await RunAsPlatformAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        SetCurrentAgency(null); // the platform administrator has no tenant

        var carId = await SendAsync(new CreateAgencyCarCommand
        {
            AgencyId = agencyId, Matricule = " 123 TU 4567 ", DailyRate = 80m
        });

        var car = await FindIgnoringFiltersAsync<Car>(c => c.Id == carId);

        car!.AgencyId.Should().Be(agencyId);
        car.Matricule.Should().Be("123 TU 4567"); // trimmed
        car.Status.Should().Be(CarStatus.Active);
        // Denominated in the agency's currency, like every stored amount.
        car.DailyRate!.Amount.Should().Be(80m);
        car.DailyRate.Currency.Should().Be("TND");
    }

    [Test]
    public async Task TheFirstCarsStillCountAgainstThePlan()
    {
        await RunAsPlatformAdministratorAsync();
        var agencyId = await AddTestAgencyAsync(maxCars: 1);
        SetCurrentAgency(null);

        await SendAsync(new CreateAgencyCarCommand { AgencyId = agencyId, Matricule = "SETUP-1" });

        // Setting an agency up must not stock it past the plan it is paying for —
        // the same refusal its own staff would meet (see SubscriptionGuard).
        await FluentActions.Invoking(() => SendAsync(new CreateAgencyCarCommand
        {
            AgencyId = agencyId, Matricule = "SETUP-2"
        })).Should().ThrowAsync<PlanLimitExceededException>();
    }

    [Test]
    public async Task InvoiceSettingsAreWrittenToTheAgency()
    {
        await RunAsPlatformAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        SetCurrentAgency(null);

        await SendAsync(new SetAgencyInvoiceSettingsCommand
        {
            AgencyId = agencyId,
            TaxIdentifier = "  1234567/A/M/000  ",
            VatRatePercent = 19m,
            FiscalStampAmount = 1m
        });

        var settings = await FindIgnoringFiltersAsync<AgencySettings>(s => s.AgencyId == agencyId);

        settings!.TaxIdentifier.Should().Be("1234567/A/M/000"); // trimmed
        settings.VatRatePercent.Should().Be(19m);
        settings.FiscalStampAmount.Should().Be(1m);
    }

    [Test]
    public async Task ARateOutsideAPercentageIsRefused()
    {
        await RunAsPlatformAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        SetCurrentAgency(null);

        await FluentActions.Invoking(() => SendAsync(new SetAgencyInvoiceSettingsCommand
        {
            AgencyId = agencyId, VatRatePercent = 120m
        })).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task AnAgencyCannotSetUpAnotherOne()
    {
        var agencyId = await AddTestAgencyAsync();
        await RunAsAgencyAdministratorAsync();

        await FluentActions.Invoking(() => SendAsync(new CreateAgencyCarCommand
        {
            AgencyId = agencyId, Matricule = "SETUP-X"
        })).Should().ThrowAsync<ForbiddenAccessException>();

        await FluentActions.Invoking(() => SendAsync(new SetAgencyInvoiceSettingsCommand
        {
            AgencyId = agencyId, VatRatePercent = 19m
        })).Should().ThrowAsync<ForbiddenAccessException>();
    }
}
