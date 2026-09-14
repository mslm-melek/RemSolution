using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Marketplace.Commands.CreateCustomerReservationCommand;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMarketplaceAgencyQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMarketplaceCarQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMarketplaceDestinationsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetShowcaseCarsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.SearchAvailableCarsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.SearchCarsMapQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Marketplace;

using static Testing;

/// <summary>
/// Being published is not enough to be on the marketplace: the agency must also
/// be entitled to be there — an active subscription, on a plan that includes
/// online reservations. Publication says "we are ready", entitlement says "we are
/// paid up and sold this"; both gates are checked on every public surface, and
/// the booking command repeats them because a car reachable by id is not hidden.
/// </summary>
public class MarketplaceEntitlementTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 3, 5, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Dob = new(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ALapsedAgencyDropsOffEveryPublicSurface()
    {
        var (agencyId, car) = await PublishedAgencyWithACarAsync("ENT-1");

        await MutateSubscriptionAsync(agencyId, s => s.Status = SubscriptionStatus.Expired);

        await AssertAbsentEverywhereAsync(agencyId, car.Id);
    }

    // The period is what lapses first in practice: the platform admin flips the
    // status later, and the marketplace must not wait for them.
    [Test]
    public async Task AnAgencyWhosePeriodRanOutDropsOffToo()
    {
        var (agencyId, car) = await PublishedAgencyWithACarAsync("ENT-2");

        await MutateSubscriptionAsync(agencyId, s =>
        {
            s.StartDate = DateTimeOffset.UtcNow.AddDays(-60);
            s.EndDate = DateTimeOffset.UtcNow.AddDays(-30);
        });

        await AssertAbsentEverywhereAsync(agencyId, car.Id);
    }

    [Test]
    public async Task AnAgencyWithoutTheOnlineReservationsFeatureIsNotOnTheMarketplace()
    {
        var (agencyId, car) = await PublishedAgencyWithACarAsync("ENT-3");

        await AddAsync(new AgencyFeature
        {
            AgencyId = agencyId,
            Feature = FeatureFlags.OnlineReservations,
            Enabled = false
        });

        await AssertAbsentEverywhereAsync(agencyId, car.Id);
    }

    // The override decides the feature, never the billing: otherwise switching a
    // module on would quietly undo the freeze.
    [Test]
    public async Task AnOverrideCannotPutALapsedAgencyBackOnTheMarketplace()
    {
        var (agencyId, car) = await PublishedAgencyWithACarAsync("ENT-4");

        await AddAsync(new AgencyFeature
        {
            AgencyId = agencyId,
            Feature = FeatureFlags.OnlineReservations,
            Enabled = true
        });

        await MutateSubscriptionAsync(agencyId, s => s.Status = SubscriptionStatus.Expired);

        await AssertAbsentEverywhereAsync(agencyId, car.Id);
    }

    [Test]
    public async Task AnEntitledAgencyIsStillListed()
    {
        var (agencyId, car) = await PublishedAgencyWithACarAsync("ENT-5");

        SetCurrentAgency(null);

        (await SendAsync(new SearchAvailableCarsQuery(Start, End))).Items.Should().HaveCount(1);
        (await SendAsync(new GetMarketplaceCarQuery(car.Id))).Should().NotBeNull();
        (await SendAsync(new GetMarketplaceAgencyQuery(agencyId))).Should().NotBeNull();
    }

    // ------------------------------------------------------------- helpers ---

    /// <summary>Published, subscribed, and selling one car — the happy state.</summary>
    private static async Task<(int AgencyId, Car Car)> PublishedAgencyWithACarAsync(string plate)
    {
        var agencyId = await AddTestAgencyAsync();

        var car = new Car
        {
            Matricule = plate, Status = CarStatus.Active, DailyRate = Money.Of(55m, "TND")
        };
        await AddAsync(car);

        return (agencyId, car);
    }

    /// <summary>
    /// Every public read, in one place: a gate that holds on the search but not on
    /// the map, the showcase or a direct link is not a gate.
    /// </summary>
    private static async Task AssertAbsentEverywhereAsync(int agencyId, int carId)
    {
        SetCurrentAgency(null);

        (await SendAsync(new SearchAvailableCarsQuery(Start, End))).Items.Should().BeEmpty();
        (await SendAsync(new SearchCarsMapQuery(Start, End))).Should().BeEmpty();
        (await SendAsync(new GetShowcaseCarsQuery())).Should().BeEmpty();
        (await SendAsync(new GetMarketplaceDestinationsQuery())).Should().BeEmpty();
        (await SendAsync(new GetMarketplaceCarQuery(carId))).Should().BeNull();

        // Missing rather than empty, like an unpublished agency.
        (await SendAsync(new GetMarketplaceAgencyQuery(agencyId))).Should().BeNull();

        // And not bookable by id either. ValidationException is the assertion that
        // matters: the save runs under the car agency's tenant, so an unguarded
        // booking throws SubscriptionRequiredException instead and the customer is
        // shown a 402 about the agency's billing, which they read as their own.
        await RunAsUserAsync($"ent-cust-{carId}@local", "Customer1234!", new[] { Roles.Customer });
        SetCurrentAgency(null);

        await FluentActions.Invoking(() => SendAsync(new CreateCustomerReservationCommand
        {
            CarId = carId, StartDate = Start, EndDate = End,
            FirstName = "Jane", LastName = "Doe", BirthDate = Dob
        })).Should().ThrowAsync<ValidationException>();
    }
}
