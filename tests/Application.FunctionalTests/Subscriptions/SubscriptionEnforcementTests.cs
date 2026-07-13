using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Car.Commands.CreateCarCommand;
using RemSolution.Application.Features.Car.Queries.GetCarsWithPaginationQuery;
using RemSolution.Application.Features.Client.Commands.CreateClientCommand;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Infrastructure.Data;
using RemSolution.Infrastructure.Data.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Application.FunctionalTests.Subscriptions;

using static Testing;

public class SubscriptionEnforcementTests : BaseTestFixture
{
    [Test]
    public async Task ShouldBlockCarCreationBeyondPlanLimit()
    {
        await RunAsDefaultUserAsync();
        await AddTestAgencyAsync(maxCars: 2);
        var modelId = await AddTestModelAsync();

        await SendAsync(MakeCar("LIM-001", modelId));
        await SendAsync(MakeCar("LIM-002", modelId));

        await FluentActions.Invoking(() =>
            SendAsync(MakeCar("LIM-003", modelId))).Should().ThrowAsync<PlanLimitExceededException>();

        (await CountAsync<Car>()).Should().Be(2);
    }

    [Test]
    public async Task ShouldBlockClientCreationBeyondPlanLimit()
    {
        await RunAsDefaultUserAsync();
        await AddTestAgencyAsync(maxClients: 1);

        await SendAsync(MakeClient("One"));

        await FluentActions.Invoking(() =>
            SendAsync(MakeClient("Two"))).Should().ThrowAsync<PlanLimitExceededException>();

        (await CountAsync<Client>()).Should().Be(1);
    }

    [Test]
    public async Task ShouldBlockWritesWithoutAnySubscription()
    {
        await RunAsDefaultUserAsync();
        await AddTestAgencyAsync(withSubscription: false);
        var modelId = await AddTestModelAsync();

        await FluentActions.Invoking(() =>
            SendAsync(MakeCar("NOSUB-01", modelId))).Should().ThrowAsync<SubscriptionRequiredException>();
    }

    [Test]
    public async Task ShouldBlockWritesButKeepReadsWhenSubscriptionExpired()
    {
        await RunAsDefaultUserAsync();
        var agencyId = await AddTestAgencyAsync();
        var modelId = await AddTestModelAsync();

        var carId = await SendAsync(MakeCar("EXP-001", modelId));

        await MutateSubscriptionAsync(agencyId, s => s.Status = SubscriptionStatus.Expired);

        // Creates are blocked...
        await FluentActions.Invoking(() =>
            SendAsync(MakeCar("EXP-002", modelId))).Should().ThrowAsync<SubscriptionRequiredException>();

        // ...and so is any other tenant write (interceptor, not just handlers).
        var car = await FindAsync<Car>(carId);
        car!.Color = "Blue";
        await FluentActions.Invoking(() =>
            UpdateAsync(car)).Should().ThrowAsync<SubscriptionRequiredException>();

        // Reads keep working.
        var page = await SendAsync(new GetCarsWithPaginationQuery { PageNumber = 1, PageSize = 10 });
        page.TotalCount.Should().Be(1);
    }

    [Test]
    public async Task ShouldBlockWritesWhenSubscriptionPeriodLapsed()
    {
        await RunAsDefaultUserAsync();
        var agencyId = await AddTestAgencyAsync();
        var modelId = await AddTestModelAsync();

        // Status still Active, but the period is over: writes must be blocked
        // even before the platform admin flips the status.
        await MutateSubscriptionAsync(agencyId, s =>
        {
            s.StartDate = DateTimeOffset.UtcNow.AddDays(-60);
            s.EndDate = DateTimeOffset.UtcNow.AddDays(-30);
        });

        await FluentActions.Invoking(() =>
            SendAsync(MakeCar("LAPSE-01", modelId))).Should().ThrowAsync<SubscriptionRequiredException>();
    }

    [Test]
    public async Task ShouldNotExceedCarLimitUnderConcurrentCreates()
    {
        const int maxCars = 3;
        const int attempts = 10;

        var userId = await RunAsDefaultUserAsync();
        var agencyId = await AddTestAgencyAsync(maxCars: maxCars);

        // The factory shares a single DbConnection across scopes, which cannot
        // carry parallel transactions — build one context per attempt with its
        // own connection so the creates genuinely race on the applock.
        var results = await Task.WhenAll(Enumerable.Range(0, attempts).Select(async i =>
        {
            var tenant = Mock.Of<ITenantProvider>(t => t.AgencyId == (int?)agencyId);

            await using var context = CreateIsolatedContext(tenant, userId);

            var handler = new CreateCarCommandHandler(context, tenant, TimeProvider.System);

            try
            {
                await handler.Handle(new CreateCarCommand
                {
                    Matricule = $"RACE-{i:D3}",
                    FirstCirculationDate = DateTime.UtcNow
                }, CancellationToken.None);

                return true;
            }
            catch (PlanLimitExceededException)
            {
                return false;
            }
        }));

        results.Count(created => created).Should().Be(maxCars,
            "count + insert run under the per-agency applock, so concurrent creates must not oversell the plan");
        (await CountAsync<Car>()).Should().Be(maxCars);
    }

    private static ApplicationDbContext CreateIsolatedContext(ITenantProvider tenant, string? userId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(GetConnectionString(), x => x.UseNetTopologySuite())
            .AddInterceptors(
                new BaseEntityInterceptor(Mock.Of<IUser>(u => u.Id == userId), TimeProvider.System),
                new TenantEntityInterceptor(tenant),
                new SubscriptionEnforcementInterceptor(tenant, TimeProvider.System))
            .Options;

        return new ApplicationDbContext(options, tenant);
    }

    private static async Task<int> AddTestModelAsync()
    {
        var brand = new Brand { Name = "Tesla" };
        await AddAsync(brand);

        var model = new ModelCar { Name = "Model 3", BrandId = brand.Id };
        await AddAsync(model);

        return model.Id;
    }

    private static CreateCarCommand MakeCar(string matricule, int modelId) => new()
    {
        Matricule = matricule,
        ModelId = modelId,
        FirstCirculationDate = DateTime.UtcNow
    };

    private static CreateClientCommand MakeClient(string firstName) => new()
    {
        FirstName = firstName,
        LastName = "Tester",
        BirthDate = new DateTime(1990, 5, 20)
    };
}
