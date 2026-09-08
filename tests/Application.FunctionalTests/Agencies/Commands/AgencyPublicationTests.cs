using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Agency.Commands.SetAgencyPublicationCommand;
using RemSolution.Application.Features.Agency.Queries.GetAgencyPublicationQuery;
using RemSolution.Application.Features.Marketplace.Commands.CreateCustomerReservationCommand;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMarketplaceAgencyQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMarketplaceCarQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMarketplaceDestinationsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyReservationsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetShowcaseCarsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.SearchAvailableCarsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.SearchCarsMapQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Agencies.Commands;

using static Testing;

/// <summary>
/// Publication: an agency reaches the public marketplace only once somebody says
/// so. The gate has to hold on every public surface, because a car that is
/// hidden from the search but reachable by id is not hidden at all.
/// </summary>
public class AgencyPublicationTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 6, 5, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Dob = new(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    // ------------------------------------------------------------ the gate ---

    [Test]
    public async Task AnUnpublishedAgencyIsAbsentFromEveryPublicList()
    {
        var car = await UnpublishedAgencyWithACarAsync();

        SetCurrentAgency(null);

        (await SendAsync(new SearchAvailableCarsQuery(Start, End))).Items.Should().BeEmpty();
        (await SendAsync(new SearchCarsMapQuery(Start, End))).Should().BeEmpty();
        (await SendAsync(new GetShowcaseCarsQuery())).Should().BeEmpty();
        (await SendAsync(new GetMarketplaceDestinationsQuery())).Should().BeEmpty();

        // And not reachable by id either — the hole a duplicated predicate leaves.
        (await SendAsync(new GetMarketplaceCarQuery(car.Id))).Should().BeNull();
    }

    [Test]
    public async Task AnUnpublishedAgencyHasNoShopfront()
    {
        var car = await UnpublishedAgencyWithACarAsync();
        var agencyId = car.AgencyId;

        SetCurrentAgency(null);

        // Missing rather than empty: it is not a shopfront with nothing in it.
        (await SendAsync(new GetMarketplaceAgencyQuery(agencyId))).Should().BeNull();
    }

    [Test]
    public async Task AnUnpublishedAgencysCarCannotBeBooked()
    {
        var car = await UnpublishedAgencyWithACarAsync();

        await RunAsUserAsync("pub-cust@local", "Customer1234!", new[] { Roles.Customer });
        SetCurrentAgency(null);

        await FluentActions.Invoking(() => SendAsync(new CreateCustomerReservationCommand
        {
            CarId = car.Id, StartDate = Start, EndDate = End,
            FirstName = "Jane", LastName = "Doe", BirthDate = Dob
        })).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task PublishingPutsTheFleetInTheWindow()
    {
        var car = await UnpublishedAgencyWithACarAsync();

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);
        await SendAsync(new SetAgencyPublicationCommand(car.AgencyId, true));

        (await SendAsync(new SearchAvailableCarsQuery(Start, End))).Items.Should().ContainSingle();
        (await SendAsync(new GetMarketplaceCarQuery(car.Id))).Should().NotBeNull();
        (await SendAsync(new GetMarketplaceAgencyQuery(car.AgencyId))).Should().NotBeNull();
    }

    // ------------------------------------------------------ going live ---

    [Test]
    public async Task AnAgencyWithNothingToOfferCannotGoLive()
    {
        await RunAsPlatformAdministratorAsync();
        var agencyId = await AddTestAgencyAsync(published: false);
        SetCurrentAgency(null);

        // An empty shopfront is worse than none.
        await FluentActions.Invoking(() =>
                SendAsync(new SetAgencyPublicationCommand(agencyId, true)))
            .Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ACarNobodyCanBookDoesNotCountAsSomethingToOffer()
    {
        await RunAsPlatformAdministratorAsync();
        var agencyId = await AddTestAgencyAsync(published: false);

        // Off the road, and unpriced: neither would ever appear in the search.
        await AddAsync(new Car { Matricule = "PUB-OFF", Status = CarStatus.Maintenance, DailyRate = Money.Of(50m, "TND") });
        await AddAsync(new Car { Matricule = "PUB-FREE", Status = CarStatus.Active });

        SetCurrentAgency(null);

        await FluentActions.Invoking(() =>
                SendAsync(new SetAgencyPublicationCommand(agencyId, true)))
            .Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task PublishingTwiceKeepsTheDayItWentLive()
    {
        var car = await UnpublishedAgencyWithACarAsync();

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);

        await SendAsync(new SetAgencyPublicationCommand(car.AgencyId, true));
        var first = (await FindAsync<Agency>(car.AgencyId))!.PublishedAt;

        await SendAsync(new SetAgencyPublicationCommand(car.AgencyId, true));

        // "Since when has this agency been on the marketplace" has one answer.
        (await FindAsync<Agency>(car.AgencyId))!.PublishedAt.Should().Be(first);
    }

    [Test]
    public async Task TakingAnAgencyOffTheMarketplaceLeavesItsBookingsAlone()
    {
        var customerId = await RunAsUserAsync("pub-cust2@local", "Customer1234!", new[] { Roles.Customer });
        var agencyId = await AddTestAgencyAsync();
        var car = new Car { Matricule = "PUB-BOOKED", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") };
        await AddAsync(car);
        SetCurrentAgency(null);

        var reservationId = await SendAsync(new CreateCustomerReservationCommand
        {
            CarId = car.Id, StartDate = Start, EndDate = End,
            FirstName = "Jane", LastName = "Doe", BirthDate = Dob
        });

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(null);
        await SendAsync(new SetAgencyPublicationCommand(agencyId, false));

        // The listing came down; the agreement did not.
        SetCurrentUser(customerId);
        var mine = await SendAsync(new GetMyReservationsQuery());

        mine.Should().ContainSingle().Which.Id.Should().Be(reservationId);
        (await SendAsync(new SearchAvailableCarsQuery(Start, End))).Items.Should().BeEmpty();
    }

    [Test]
    public async Task OnlyThePlatformDecidesWhoIsOnTheMarketplace()
    {
        var agencyId = await AddTestAgencyAsync();
        await RunAsAgencyAdministratorAsync();

        await FluentActions.Invoking(() =>
                SendAsync(new SetAgencyPublicationCommand(agencyId, false)))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    // -------------------------------------------------------- readiness ---

    [Test]
    public async Task ReadinessSaysWhatTheAgencyHasToShow()
    {
        await RunAsPlatformAdministratorAsync();
        var agencyId = await AddTestAgencyAsync(published: false);

        await AddAsync(new Car { Matricule = "PUB-R1", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") });
        await AddAsync(new Car { Matricule = "PUB-R2", Status = CarStatus.Inactive, DailyRate = Money.Of(50m, "TND") });

        SetCurrentAgency(null);

        var before = await SendAsync(new GetAgencyPublicationQuery(agencyId));

        before.IsPublished.Should().BeFalse();
        before.PublishedAt.Should().BeNull();
        // The gap between the two counts is the useful part: one of these cars
        // would never appear.
        before.TotalCars.Should().Be(2);
        before.OfferedCars.Should().Be(1);
        before.CanPublish.Should().BeTrue();

        await SendAsync(new SetAgencyPublicationCommand(agencyId, true));

        var after = await SendAsync(new GetAgencyPublicationQuery(agencyId));
        after.IsPublished.Should().BeTrue();
        after.PublishedAt.Should().NotBeNull();
    }

    // ----------------------------------------------------------- setup ---

    /// <summary>An agency still being set up, with one car that would sell.</summary>
    private static async Task<Car> UnpublishedAgencyWithACarAsync()
    {
        await AddTestAgencyAsync(published: false);

        var car = new Car
        {
            Matricule = "PUB-1", Status = CarStatus.Active, DailyRate = Money.Of(60m, "TND")
        };
        await AddAsync(car);

        return car;
    }
}
