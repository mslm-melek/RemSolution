using RemSolution.Application.Common.Settings;
using RemSolution.Application.Features.Car.Commands.CreateCarCommand;
using RemSolution.Domain.Entities;
using RemSolution.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace RemSolution.Application.FunctionalTests.Agencies;

using static Testing;

public class AgencySettingsTests : BaseTestFixture
{
    [Test]
    public async Task ANewCarPricesInTheAgencySettingsCurrency()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync(); // settings currency = TND

        // Move the agency to EUR through its settings row.
        await UsingScopeAsync<bool>(async sp =>
        {
            var context = sp.GetRequiredService<ApplicationDbContext>();
            var settings = await context.AgencySettings.FirstAsync(s => s.AgencyId == agencyId);
            settings.CurrencyCode = "EUR";
            await context.SaveChangesAsync();
            return true;
        });

        var brand = new Brand { Name = "Renault" };
        await AddAsync(brand);
        var model = new ModelCar { Name = "Clio", BrandId = brand.Id };
        await AddAsync(model);

        var carId = await SendAsync(new CreateCarCommand
        {
            Matricule = "CUR-1",
            ModelId = model.Id,
            DailyRate = 120m,
            FirstCirculationDate = DateTime.UtcNow
        });

        // The rate's currency was resolved from the agency's settings, not the client.
        var car = await FindAsync<Car>(carId);
        car!.DailyRate!.Currency.Should().Be("EUR");
        car.DailyRate.Amount.Should().Be(120m);
    }

    [Test]
    public async Task SettingsProviderCachesUntilInvalidated()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyId = await AddTestAgencyAsync(); // TND

        (await GetSettingsAsync(agencyId)).CurrencyCode.Should().Be("TND");

        // Change the row directly (no command → no invalidation).
        await UsingScopeAsync<bool>(async sp =>
        {
            var context = sp.GetRequiredService<ApplicationDbContext>();
            var settings = await context.AgencySettings.FirstAsync(s => s.AgencyId == agencyId);
            settings.CurrencyCode = "USD";
            await context.SaveChangesAsync();
            return true;
        });

        // Still the cached snapshot (the cache is a shared singleton).
        (await GetSettingsAsync(agencyId)).CurrencyCode.Should().Be("TND");

        // After invalidation the next read reloads from the database.
        await UsingScopeAsync<bool>(sp =>
        {
            sp.GetRequiredService<IAgencySettingsProvider>().Invalidate(agencyId);
            return Task.FromResult(true);
        });

        (await GetSettingsAsync(agencyId)).CurrencyCode.Should().Be("USD");
    }

    private static Task<AgencySettingsSnapshot> GetSettingsAsync(int agencyId) =>
        UsingScopeAsync(sp => sp.GetRequiredService<IAgencySettingsProvider>().GetAsync(agencyId));
}
