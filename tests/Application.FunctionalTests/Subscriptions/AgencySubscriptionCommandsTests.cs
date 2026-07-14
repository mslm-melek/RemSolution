using RemSolution.Application.Features.AgencySubscription.Commands.AssignAgencySubscriptionCommand;
using RemSolution.Application.Features.AgencySubscription.Queries.GetMySubscriptionQuery;
using RemSolution.Application.Features.Car.Commands.CreateCarCommand;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.FunctionalTests.Subscriptions;

using static Testing;

public class AgencySubscriptionCommandsTests : BaseTestFixture
{
    [Test]
    public async Task AssignShouldSupersedePreviousActiveSubscription()
    {
        await RunAsDefaultUserAsync();
        var agencyId = await AddTestAgencyAsync();

        var upgradedPlan = new SubscriptionPlan { Name = "Upgraded", MaxCars = 50, MaxClients = 200, Price = 99m };
        await AddAsync(upgradedPlan);

        // Platform-admin operation: requires the role, carries no tenant.
        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);

        var newSubscriptionId = await SendAsync(new AssignAgencySubscriptionCommand
        {
            AgencyId = agencyId,
            PlanId = upgradedPlan.Id,
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddYears(1)
        });

        var statuses = new List<SubscriptionStatus>();
        await MutateSubscriptionAsync(agencyId, s => statuses.Add(s.Status));

        statuses.Count(s => s == SubscriptionStatus.Active).Should().Be(1);
        statuses.Count(s => s == SubscriptionStatus.Expired).Should().Be(1);

        var active = await FindAsync<AgencySubscription>(newSubscriptionId);
        active!.Status.Should().Be(SubscriptionStatus.Active);
        active.PlanId.Should().Be(upgradedPlan.Id);
    }

    [Test]
    public async Task GetMySubscriptionShouldReportPlanAndUsage()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync(maxCars: 10, maxClients: 20);

        var brand = new Brand { Name = "Tesla" };
        await AddAsync(brand);

        var model = new ModelCar { Name = "Model 3", BrandId = brand.Id };
        await AddAsync(model);

        await SendAsync(new CreateCarCommand
        {
            Matricule = "USE-001",
            ModelId = model.Id,
            FirstCirculationDate = DateTime.UtcNow
        });

        var subscription = await SendAsync(new GetMySubscriptionQuery());

        subscription.Should().NotBeNull();
        subscription!.IsActive.Should().BeTrue();
        subscription.MaxCars.Should().Be(10);
        subscription.MaxClients.Should().Be(20);
        subscription.MaxUsers.Should().Be(100);
        subscription.CarsUsed.Should().Be(1);
        subscription.ClientsUsed.Should().Be(0);
        // The admin test account is not linked to the agency, so no users yet.
        subscription.UsersUsed.Should().Be(0);
        subscription.Status.Should().Be(SubscriptionStatus.Active);
    }
}
