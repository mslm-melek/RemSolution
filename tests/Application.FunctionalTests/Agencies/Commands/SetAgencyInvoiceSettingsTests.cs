using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Agency.Commands.SetAgencyInvoiceSettingsCommand;
using RemSolution.Application.Features.Facture.Commands.GenerateFactureCommand;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Agencies.Commands;

using static Testing;

/// <summary>
/// The tax settings a platform administrator fills in while an agency is being
/// opened. What matters here is not the write but what happens NEXT: every
/// invoice freezes its own copy of the rate at issue, so a correction that the
/// cached snapshot has not caught up with is not a stale screen — it is a
/// permanently wrong document.
/// </summary>
public class SetAgencyInvoiceSettingsTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 6, 4, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ShouldWriteTheTaxSettings()
    {
        var agencyId = await SetUpAgencyAsync();
        await RunAsPlatformAdministratorAsync();

        await SendAsync(new SetAgencyInvoiceSettingsCommand
        {
            AgencyId = agencyId,
            TaxIdentifier = "  1234567/A/M/000  ",
            VatRatePercent = 7m,
            FiscalStampAmount = 0.6m
        });

        var settings = (await AllAsync<AgencySettings>()).Single(s => s.AgencyId == agencyId);

        settings.TaxIdentifier.Should().Be("1234567/A/M/000");
        settings.VatRatePercent.Should().Be(7m);
        settings.FiscalStampAmount.Should().Be(0.6m);
    }

    [Test]
    public async Task ShouldReachTheNextInvoiceEvenWithTheSnapshotAlreadyCached()
    {
        var agencyAdmin = await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        // Warms the cache: the snapshot is read to issue this one, and it is held
        // for ten minutes.
        var first = await SendAsync(new GenerateFactureCommand
        {
            RentingId = await SeedRentingAsync("VAT-1")
        });

        (await FindAsync<Facture>(first.Id))!.VatRatePercent.Should().Be(19m);

        await RunAsPlatformAdministratorAsync();
        await SendAsync(new SetAgencyInvoiceSettingsCommand
        {
            AgencyId = agencyId,
            VatRatePercent = 7m,
            FiscalStampAmount = 1m
        });

        // Back to the agency, without re-creating the account.
        SetCurrentUser(agencyAdmin);
        SetCurrentAgency(agencyId);

        var second = await SendAsync(new GenerateFactureCommand
        {
            RentingId = await SeedRentingAsync("VAT-2")
        });

        // Without the invalidation this said 19 for another ten minutes — and a
        // reprint has to say what was charged, so those invoices stay wrong.
        (await FindAsync<Facture>(second.Id))!.VatRatePercent.Should().Be(7m);
    }

    [Test]
    public async Task ShouldBeThePlatformsToSet()
    {
        var agencyId = await SetUpAgencyAsync();

        // The agency's own screen has no tax fields (see UpdateMyAgencyCommand):
        // these three are settled when the agency is opened.
        await FluentActions.Invoking(() => SendAsync(new SetAgencyInvoiceSettingsCommand
        {
            AgencyId = agencyId, VatRatePercent = 7m, FiscalStampAmount = 1m
        })).Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task ShouldRefuseARateThatIsNotAPercentage()
    {
        var agencyId = await SetUpAgencyAsync();
        await RunAsPlatformAdministratorAsync();

        await FluentActions.Invoking(() => SendAsync(new SetAgencyInvoiceSettingsCommand
        {
            AgencyId = agencyId, VatRatePercent = 120m, FiscalStampAmount = 1m
        })).Should().ThrowAsync<ValidationException>();
    }

    // --- setup ----------------------------------------------------------------

    private static async Task<int> SetUpAgencyAsync()
    {
        await RunAsAgencyAdministratorAsync();
        return await AddTestAgencyAsync();
    }

    private static async Task<int> SeedRentingAsync(string matricule)
    {
        var car = new Car
        {
            Matricule = matricule, Status = CarStatus.Active, DailyRate = Money.Of(120m, "TND")
        };
        await AddAsync(car);

        var client = new Client
        {
            FirstName = "Tax", LastName = "Client", BirthDate = new DateTime(1990, 1, 1)
        };
        await AddAsync(client);

        var renting = RentingFixture.Hire(
            car.Id, client.Id, Start, End, price: Money.Of(360m, "TND"));

        await AddAsync(renting);
        return renting.Id;
    }
}
