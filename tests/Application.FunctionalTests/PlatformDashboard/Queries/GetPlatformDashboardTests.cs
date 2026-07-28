using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.PlatformDashboard.Queries.GetPlatformDashboardQuery;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using AgencyEntity = RemSolution.Domain.Entities.Agency;
using CarEntity = RemSolution.Domain.Entities.Car;
using ClientEntity = RemSolution.Domain.Entities.Client;

namespace RemSolution.Application.FunctionalTests.PlatformDashboard.Queries;

using static Testing;

// The app owner's overview. What it has to get right: the car/client counts come
// from EVERY agency (they are tenant data, read through the audited cross-tenant
// path), archived rows are left out, and "active" means the same thing it means
// everywhere else — status Active AND the period covering now.
public class GetPlatformDashboardTests : BaseTestFixture
{
    [Test]
    public async Task CountsCarsAndClientsAcrossEveryAgency()
    {
        await RunAsPlatformAdministratorAsync();

        // AddTestAgencyAsync also makes the harness act as that agency, so the
        // rows added after it are stamped with its AgencyId.
        var first = await AddTestAgencyAsync();
        await AddAsync(new CarEntity { Matricule = "PD-1" });
        await AddAsync(new CarEntity { Matricule = "PD-2" });
        await AddAsync(new ClientEntity { FirstName = "First", LastName = "Client" });

        var second = await AddTestAgencyAsync();
        await AddAsync(new CarEntity { Matricule = "PD-3" });

        var result = await SendAsync(new GetPlatformDashboardQuery());

        result.TotalAgencies.Should().Be(2);
        result.TotalCars.Should().Be(3);
        result.TotalClients.Should().Be(1);

        result.Agencies.Single(a => a.AgencyId == first).Cars.Should().Be(2);
        result.Agencies.Single(a => a.AgencyId == first).Clients.Should().Be(1);
        result.Agencies.Single(a => a.AgencyId == second).Cars.Should().Be(1);
        result.Agencies.Single(a => a.AgencyId == second).Clients.Should().Be(0);
    }

    [Test]
    public async Task ArchivedCarsAreLeftOutOfTheTotals()
    {
        await RunAsPlatformAdministratorAsync();
        await AddTestAgencyAsync();

        var kept = new CarEntity { Matricule = "PD-KEPT" };
        var archived = new CarEntity { Matricule = "PD-GONE" };
        await AddAsync(kept);
        await AddAsync(archived);

        // The cross-tenant queryable drops the soft-delete filter along with the
        // tenant one, so this is the case that catches a missing !IsDeleted.
        archived.IsDeleted = true;
        await UpdateAsync(archived);

        var result = await SendAsync(new GetPlatformDashboardQuery());

        result.TotalCars.Should().Be(1);
        result.Agencies.Single().Cars.Should().Be(1);
    }

    [Test]
    public async Task AgenciesInTheSameCountryShareOneBreakdownRow()
    {
        await RunAsPlatformAdministratorAsync();

        var firstId = await AddTestAgencyAsync();
        var first = await FindAsync<AgencyEntity>(firstId);

        // A second agency in the same country, with no subscription of its own.
        await AddAsync(new AgencyEntity
        {
            Name = "Same Country Agency",
            CountryId = first!.CountryId,
            Settings = new AgencySettings { CurrencyCode = "TND" }
        });

        var result = await SendAsync(new GetPlatformDashboardQuery());

        result.TotalAgencies.Should().Be(2);
        result.TotalCountries.Should().Be(1);
        result.AgenciesWithoutSubscription.Should().Be(1);

        var country = result.Countries.Single();
        country.CountryId.Should().Be(first.CountryId);
        country.Agencies.Should().Be(2);
        country.ActiveSubscriptions.Should().Be(1);
    }

    [Test]
    public async Task EachCountryGetsItsOwnRow()
    {
        await RunAsPlatformAdministratorAsync();

        // Each call seeds its own country, so this is two countries with one
        // agency each.
        await AddTestAgencyAsync();
        await AddAsync(new CarEntity { Matricule = "PD-C1" });
        await AddTestAgencyAsync();

        var result = await SendAsync(new GetPlatformDashboardQuery());

        result.TotalCountries.Should().Be(2);
        result.Countries.Should().HaveCount(2);
        result.Countries.Sum(c => c.Agencies).Should().Be(2);
        result.Countries.Sum(c => c.Cars).Should().Be(1);
    }

    [Test]
    public async Task ASubscriptionWhosePeriodHasRunOutIsLapsedNotActive()
    {
        await RunAsPlatformAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();

        // Status left at Active on purpose: the period is what blocks the agency,
        // and the screen has to surface the disagreement.
        await MutateSubscriptionAsync(agencyId, s =>
        {
            s.StartDate = DateTimeOffset.UtcNow.AddDays(-60);
            s.EndDate = DateTimeOffset.UtcNow.AddDays(-1);
        });

        var result = await SendAsync(new GetPlatformDashboardQuery());

        result.ActiveSubscriptions.Should().Be(0);
        result.LapsedSubscriptions.Should().Be(1);
        result.ActivePlanRevenue.Should().Be(0m);

        var agency = result.Agencies.Single();
        agency.SubscriptionIsActive.Should().BeFalse();
        agency.SubscriptionStatus.Should().Be(SubscriptionStatus.Active);
        result.Countries.Single().ActiveSubscriptions.Should().Be(0);
    }

    [Test]
    public async Task ActiveSubscriptionsCarryTheirPlanPriceAndLimits()
    {
        await RunAsPlatformAdministratorAsync();
        await AddTestAgencyAsync(maxCars: 5, maxClients: 7);

        var result = await SendAsync(new GetPlatformDashboardQuery());

        result.ActiveSubscriptions.Should().Be(1);
        result.LapsedSubscriptions.Should().Be(0);
        result.ActivePlanRevenue.Should().Be(49.99m); // the test plan's price

        var agency = result.Agencies.Single();
        agency.SubscriptionIsActive.Should().BeTrue();
        agency.MaxCars.Should().Be(5);
        agency.MaxClients.Should().Be(7);

        var plan = result.Plans.Single();
        plan.ActiveAgencies.Should().Be(1);
        plan.Subscriptions.Should().Be(1);
        plan.MaxCars.Should().Be(5);
    }

    [Test]
    public async Task AnAgencyThatHasFilledItsPlanIsFlagged()
    {
        await RunAsPlatformAdministratorAsync();
        await AddTestAgencyAsync(maxCars: 1, maxClients: 100);
        await AddAsync(new CarEntity { Matricule = "PD-FULL" });

        var result = await SendAsync(new GetPlatformDashboardQuery());

        result.AgenciesAtCarQuota.Should().Be(1);
        result.AgenciesAtClientQuota.Should().Be(0);
    }

    [Test]
    public async Task AnUpcomingRenewalIsCountedAsExpiringSoon()
    {
        await RunAsPlatformAdministratorAsync();
        var agencyId = await AddTestAgencyAsync();
        await MutateSubscriptionAsync(agencyId, s => s.EndDate = DateTimeOffset.UtcNow.AddDays(5));

        var soon = await SendAsync(new GetPlatformDashboardQuery());
        soon.SubscriptionsExpiringSoon.Should().Be(1);

        // A shorter notice window moves the same subscription out of the count.
        var narrow = await SendAsync(new GetPlatformDashboardQuery(ExpiringWithinDays: 2));
        narrow.SubscriptionsExpiringSoon.Should().Be(0);
    }

    [Test]
    public async Task TheCrossTenantReadIsAudited()
    {
        await RunAsPlatformAdministratorAsync();
        await AddTestAgencyAsync();

        await SendAsync(new GetPlatformDashboardQuery());

        // The counts are tenant data: the bypass must leave both audit rows
        // behind (the access register and the business trail).
        (await CountAsync<CrossTenantAccessLog>()).Should().Be(1);
        (await CountAsync<AuditLog>(a => a.Action == "CrossTenantRead")).Should().Be(1);
    }

    [Test]
    public async Task AnAgencyAdministratorIsDenied()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() => SendAsync(new GetPlatformDashboardQuery()))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }
}
