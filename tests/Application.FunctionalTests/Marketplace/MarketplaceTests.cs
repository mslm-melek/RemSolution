using NetTopologySuite.Geometries;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Marketplace.Commands.CancelMyReservationCommand;
using RemSolution.Application.Features.Marketplace.Commands.CreateCustomerReservationCommand;
using RemSolution.Application.Features.Marketplace.Commands.CreateMyReviewCommand;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetAgencyReviewsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMarketplaceAgencyQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMarketplaceDestinationsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyRentingsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyReservationsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetShowcaseCarsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.SearchAvailableCarsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.SearchCarsMapQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;

namespace RemSolution.Application.FunctionalTests.Marketplace;

using static Testing;

public class MarketplaceTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 1, 4, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Dob = new(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task SearchReturnsAvailableCarsAcrossAgencies()
    {
        var agencyA = await AddTestAgencyAsync();
        await AddAsync(new Car { Matricule = "MK-A", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") });

        var agencyB = await AddTestAgencyAsync(); // switches current tenant to B
        await AddAsync(new Car { Matricule = "MK-B", Status = CarStatus.Active, DailyRate = Money.Of(60m, "TND") });

        agencyA.Should().NotBe(agencyB);

        var result = await SendAsync(new SearchAvailableCarsQuery(Start, End));

        result.TotalCount.Should().Be(2); // one from each agency
    }

    [Test]
    public async Task SearchExcludesInactiveUnpricedAndBookedCars()
    {
        await AddTestAgencyAsync();
        var available = new Car { Matricule = "MK-OK", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") };
        await AddAsync(available);
        await AddAsync(new Car { Matricule = "MK-MAINT", Status = CarStatus.Maintenance, DailyRate = Money.Of(50m, "TND") });
        await AddAsync(new Car { Matricule = "MK-NOPRICE", Status = CarStatus.Active, DailyRate = null });

        var booked = new Car { Matricule = "MK-BOOKED", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") };
        await AddAsync(booked);
        await AddAsync(Reservation.Create(
            booked.Id, Start, End, price: null, expiresAt: Start.AddHours(-1)));

        var result = await SendAsync(new SearchAvailableCarsQuery(Start, End));

        result.TotalCount.Should().Be(1); // only the available, priced, unbooked car
        result.Items.First().Matricule.Should().Be("MK-OK");
    }

    [Test]
    public async Task SearchExcludesCarsWithAnOverlappingActiveRenting()
    {
        await AddTestAgencyAsync();

        var rented = new Car { Matricule = "MK-RENT", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") };
        await AddAsync(rented);
        await AddAsync(new Renting
        {
            CarId = rented.Id, StartDate = Start, EndDate = End, RentingState = RentingState.InProgress
        });

        var free = new Car { Matricule = "MK-FREE", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") };
        await AddAsync(free);
        // A completed renting is terminal and must NOT block availability.
        await AddAsync(new Renting
        {
            CarId = free.Id, StartDate = Start, EndDate = End, RentingState = RentingState.Done
        });

        var result = await SendAsync(new SearchAvailableCarsQuery(Start, End));

        result.TotalCount.Should().Be(1);
        result.Items.First().Matricule.Should().Be("MK-FREE");
    }

    [Test]
    public async Task SearchFiltersByAgency()
    {
        var agencyA = await AddTestAgencyAsync();
        await AddAsync(new Car { Matricule = "MK-A", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") });

        await AddTestAgencyAsync(); // switches current tenant
        await AddAsync(new Car { Matricule = "MK-B", Status = CarStatus.Active, DailyRate = Money.Of(60m, "TND") });

        var result = await SendAsync(new SearchAvailableCarsQuery(Start, End, AgencyId: agencyA));

        result.TotalCount.Should().Be(1);
        result.Items.First().Matricule.Should().Be("MK-A");
    }

    [Test]
    public async Task SearchByCountryMatchesTheBranchCountry()
    {
        await AddTestAgencyAsync();

        var country = new Country { Name = "Marketland" };
        await AddAsync(country);
        var branch = new Branch { Name = "Airport", CountryId = country.Id };
        await AddAsync(branch);

        await AddAsync(new Car
        {
            Matricule = "MK-BR", Status = CarStatus.Active,
            DailyRate = Money.Of(50m, "TND"), BranchId = branch.Id
        });
        // Same agency, no branch ⇒ a different country (the agency's own).
        await AddAsync(new Car { Matricule = "MK-NB", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") });

        var result = await SendAsync(new SearchAvailableCarsQuery(Start, End, CountryId: country.Id));

        result.TotalCount.Should().Be(1);
        result.Items.First().Matricule.Should().Be("MK-BR");
    }

    [Test]
    public async Task SearchByCountryFallsBackToTheAgencyCountryForABranchlessCar()
    {
        var agencyId = await AddTestAgencyAsync();
        var agency = await FindIgnoringFiltersAsync<Agency>(a => a.Id == agencyId);

        await AddAsync(new Car { Matricule = "MK-NB", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") });

        var result = await SendAsync(new SearchAvailableCarsQuery(Start, End, CountryId: agency!.CountryId));

        result.TotalCount.Should().Be(1);
        result.Items.First().Matricule.Should().Be("MK-NB");
    }

    [Test]
    public async Task DestinationsCountOnlyCarsOnOfferAndListTheirPlaces()
    {
        var agencyId = await AddTestAgencyAsync();

        var country = new Country { Name = "Marketland" };
        await AddAsync(country);
        var branch = new Branch { Name = "Airport", CountryId = country.Id };
        await AddAsync(branch);

        await AddAsync(new Car
        {
            Matricule = "MK-1", Status = CarStatus.Active,
            DailyRate = Money.Of(50m, "TND"), BranchId = branch.Id
        });
        await AddAsync(new Car
        {
            Matricule = "MK-2", Status = CarStatus.Active,
            DailyRate = Money.Of(70m, "TND"), BranchId = branch.Id
        });
        // Neither is on offer, so neither may be counted.
        await AddAsync(new Car
        {
            Matricule = "MK-MAINT", Status = CarStatus.Maintenance,
            DailyRate = Money.Of(50m, "TND"), BranchId = branch.Id
        });
        await AddAsync(new Car
        {
            Matricule = "MK-NOPRICE", Status = CarStatus.Active,
            DailyRate = null, BranchId = branch.Id
        });

        var destinations = await SendAsync(new GetMarketplaceDestinationsQuery());

        var marketland = destinations.Single(d => d.CountryName == "Marketland");
        marketland.CountryId.Should().Be(country.Id);
        marketland.CarCount.Should().Be(2);
        marketland.Places.Should().HaveCount(1);
        marketland.Places[0].BranchId.Should().Be(branch.Id);
        marketland.Places[0].Name.Should().Be("Airport");
        marketland.Places[0].AgencyId.Should().Be(agencyId);
        marketland.Places[0].AgencyName.Should().Be("Test Agency");
        marketland.Places[0].CarCount.Should().Be(2);
    }

    [Test]
    public async Task DestinationsCountABranchlessCarUnderItsAgencyCountry()
    {
        var agencyId = await AddTestAgencyAsync();
        var agency = await FindIgnoringFiltersAsync<Agency>(a => a.Id == agencyId);

        await AddAsync(new Car { Matricule = "MK-NB", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") });

        var destinations = await SendAsync(new GetMarketplaceDestinationsQuery());

        var home = destinations.Single(d => d.CountryId == agency!.CountryId);
        home.CarCount.Should().Be(1);
        // A car with no branch belongs to no place.
        home.Places.Should().BeEmpty();
    }

    [Test]
    public async Task AgencyShopfrontReportsTheCheapestOfferAndItsPlaces()
    {
        var agencyId = await AddTestAgencyAsync();

        var country = new Country { Name = "Marketland" };
        await AddAsync(country);
        var branch = new Branch { Name = "Airport", CountryId = country.Id };
        await AddAsync(branch);

        await AddAsync(new Car
        {
            Matricule = "MK-CHEAP", Status = CarStatus.Active,
            DailyRate = Money.Of(45m, "TND"), BranchId = branch.Id
        });
        await AddAsync(new Car
        {
            Matricule = "MK-DEAR", Status = CarStatus.Active,
            DailyRate = Money.Of(180m, "TND"), BranchId = branch.Id
        });
        // Not on offer, so it counts neither towards the fleet nor the price.
        await AddAsync(new Car
        {
            Matricule = "MK-OFF", Status = CarStatus.Inactive,
            DailyRate = Money.Of(10m, "TND"), BranchId = branch.Id
        });

        var shopfront = await SendAsync(new GetMarketplaceAgencyQuery(agencyId));

        shopfront.Should().NotBeNull();
        shopfront!.Name.Should().Be("Test Agency");
        shopfront.CarCount.Should().Be(2);
        shopfront.FromDailyRate!.Amount.Should().Be(45m);
        shopfront.FromDailyRate.Currency.Should().Be("TND");
        shopfront.Places.Should().HaveCount(1);
        shopfront.Places[0].CarCount.Should().Be(2);
    }

    [Test]
    public async Task AgencyShopfrontIsNullForAnUnknownAgency()
    {
        await AddTestAgencyAsync();

        var shopfront = await SendAsync(new GetMarketplaceAgencyQuery(9_999));

        shopfront.Should().BeNull();
    }

    [Test]
    public async Task ShowcaseReturnsOnlyCarsOnOfferUpToTheRequestedCount()
    {
        await AddTestAgencyAsync();

        await AddAsync(new Car { Matricule = "MK-1", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") });
        await AddAsync(new Car { Matricule = "MK-2", Status = CarStatus.Active, DailyRate = Money.Of(60m, "TND") });
        await AddAsync(new Car { Matricule = "MK-3", Status = CarStatus.Active, DailyRate = Money.Of(70m, "TND") });
        await AddAsync(new Car { Matricule = "MK-OFF", Status = CarStatus.Inactive, DailyRate = Money.Of(80m, "TND") });
        await AddAsync(new Car { Matricule = "MK-NOPRICE", Status = CarStatus.Active, DailyRate = null });

        var all = await SendAsync(new GetShowcaseCarsQuery(8));
        all.Should().HaveCount(3);
        all.Select(c => c.Matricule).Should().NotContain("MK-OFF").And.NotContain("MK-NOPRICE");

        var two = await SendAsync(new GetShowcaseCarsQuery(2));
        two.Should().HaveCount(2);
    }

    [Test]
    public async Task ShowcaseIgnoresWhetherACarIsBookedForAnyParticularWindow()
    {
        await AddTestAgencyAsync();

        var booked = new Car { Matricule = "MK-BOOKED", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") };
        await AddAsync(booked);
        await AddAsync(new Renting
        {
            CarId = booked.Id, StartDate = Start, EndDate = End, RentingState = RentingState.InProgress
        });

        // The shop window advertises the fleet; the dates are chosen on /browse.
        var showcase = await SendAsync(new GetShowcaseCarsQuery());

        showcase.Should().HaveCount(1);
    }

    [Test]
    public async Task CustomerBookingCreatesPendingHoldAndClientInTheCarsAgency()
    {
        var customerId = await RunAsUserAsync("cust@local", "Customer1234!", new[] { Roles.Customer });
        var agencyId = await AddTestAgencyAsync();
        var car = new Car { Matricule = "MK-1", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") };
        await AddAsync(car);
        SetCurrentAgency(null); // a customer has no tenant of their own

        var reservationId = await SendAsync(new CreateCustomerReservationCommand
        {
            CarId = car.Id, StartDate = Start, EndDate = End,
            FirstName = "Jane", LastName = "Doe", BirthDate = Dob
        });

        var reservation = await FindIgnoringFiltersAsync<Reservation>(r => r.Id == reservationId);
        reservation!.AgencyId.Should().Be(agencyId);
        reservation.Status.Should().Be(ReservationStatus.PendingConfirmation);
        reservation.ExpiresAt.Should().NotBeNull();
        reservation.Price!.Amount.Should().Be(150m); // 3 days × 50

        var client = await FindIgnoringFiltersAsync<Client>(c => c.Id == reservation.ClientId);
        client!.MarketplaceUserId.Should().Be(customerId);
        client.AgencyId.Should().Be(agencyId);
        client.FirstName.Should().Be("Jane");
    }

    [Test]
    public async Task GetMyReservationsReturnsTheCustomersOwnHolds()
    {
        await RunAsUserAsync("cust2@local", "Customer1234!", new[] { Roles.Customer });
        await AddTestAgencyAsync();
        var car = new Car { Matricule = "MK-2", Status = CarStatus.Active, DailyRate = Money.Of(40m, "TND") };
        await AddAsync(car);
        SetCurrentAgency(null);

        await SendAsync(new CreateCustomerReservationCommand
        {
            CarId = car.Id, StartDate = Start, EndDate = End,
            FirstName = "Sam", LastName = "Lee", BirthDate = Dob
        });

        var mine = await SendAsync(new GetMyReservationsQuery());

        mine.Should().HaveCount(1);
        mine[0].Status.Should().Be(ReservationStatus.PendingConfirmation);
        mine[0].AgencyName.Should().Be("Test Agency");
    }

    [Test]
    public async Task CustomerCanCancelOwnPendingReservation()
    {
        await RunAsUserAsync("cust3@local", "Customer1234!", new[] { Roles.Customer });
        await AddTestAgencyAsync();
        var car = new Car { Matricule = "MK-3", Status = CarStatus.Active, DailyRate = Money.Of(40m, "TND") };
        await AddAsync(car);
        SetCurrentAgency(null);

        var reservationId = await SendAsync(new CreateCustomerReservationCommand
        {
            CarId = car.Id, StartDate = Start, EndDate = End,
            FirstName = "Sam", LastName = "Lee", BirthDate = Dob
        });

        await SendAsync(new CancelMyReservationCommand(reservationId));

        var reservation = await FindIgnoringFiltersAsync<Reservation>(r => r.Id == reservationId);
        reservation!.Status.Should().Be(ReservationStatus.Cancelled);
    }

    [Test]
    public async Task NonCustomerCannotBook()
    {
        await RunAsAgencyAdministratorAsync(); // not a Customer
        await AddTestAgencyAsync();
        var car = new Car { Matricule = "MK-4", Status = CarStatus.Active, DailyRate = Money.Of(40m, "TND") };
        await AddAsync(car);

        await FluentActions.Invoking(() => SendAsync(new CreateCustomerReservationCommand
        {
            CarId = car.Id, StartDate = Start, EndDate = End,
            FirstName = "X", LastName = "Y", BirthDate = Dob
        })).Should().ThrowAsync<ForbiddenAccessException>();
    }

    // ------------------------------------------------------------- ratings ---

    [Test]
    public async Task CustomerCanRateAFinishedRenting()
    {
        var (customerId, agencyId, renting) = await FinishedRentingAsync("rate1@local");

        var reviewId = await SendAsync(new CreateMyReviewCommand
        {
            RentingId = renting.Id, Rating = 5, Comment = "  Spotless car, easy pickup.  "
        });

        var review = await FindIgnoringFiltersAsync<AgencyReview>(r => r.Id == reviewId);
        review!.AgencyId.Should().Be(agencyId);
        review.RentingId.Should().Be(renting.Id);
        review.Rating.Should().Be(5);
        review.AuthorUserId.Should().Be(customerId);
        review.AuthorName.Should().Be("Ann Bee");
        review.Comment.Should().Be("Spotless car, easy pickup."); // trimmed
        review.SubmittedAt.Should().NotBe(default);
    }

    [Test]
    public async Task RatingRequiresTheRentingToBeFinished()
    {
        var (_, _, renting) = await FinishedRentingAsync("rate2@local", RentingState.InProgress);

        await FluentActions.Invoking(() => SendAsync(new CreateMyReviewCommand
        {
            RentingId = renting.Id, Rating = 4
        })).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ARentingCanOnlyBeRatedOnce()
    {
        var (_, _, renting) = await FinishedRentingAsync("rate3@local");

        await SendAsync(new CreateMyReviewCommand { RentingId = renting.Id, Rating = 4 });

        await FluentActions.Invoking(() => SendAsync(new CreateMyReviewCommand
        {
            RentingId = renting.Id, Rating = 1
        })).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ACustomerCannotRateSomeoneElsesRenting()
    {
        var (_, _, renting) = await FinishedRentingAsync("owner@local");

        // A different marketplace account: someone else's renting is
        // indistinguishable from one that does not exist.
        await RunAsUserAsync("stranger@local", "Customer1234!", new[] { Roles.Customer });
        SetCurrentAgency(null);

        await FluentActions.Invoking(() => SendAsync(new CreateMyReviewCommand
        {
            RentingId = renting.Id, Rating = 1
        })).Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task MyRentingsReportsWhatCanStillBeRated()
    {
        var (_, _, renting) = await FinishedRentingAsync("rate4@local");

        var before = await SendAsync(new GetMyRentingsQuery());
        before.Should().HaveCount(1);
        before[0].CanReview.Should().BeTrue();
        before[0].MyRating.Should().BeNull();

        await SendAsync(new CreateMyReviewCommand { RentingId = renting.Id, Rating = 3, Comment = "Fine." });

        var after = await SendAsync(new GetMyRentingsQuery());
        after[0].CanReview.Should().BeFalse();
        after[0].MyRating.Should().Be(3);
        after[0].MyComment.Should().Be("Fine.");
    }

    [Test]
    public async Task AgencyShopfrontAveragesItsRatingsAndBreaksThemDown()
    {
        var (_, agencyId, _) = await FinishedRentingAsync("rate5@local");
        await AddReviewAsync(agencyId, 5);
        await AddReviewAsync(agencyId, 4);
        await AddReviewAsync(agencyId, 3);

        var shopfront = await SendAsync(new GetMarketplaceAgencyQuery(agencyId));

        shopfront!.Rating.ReviewCount.Should().Be(3);
        shopfront.Rating.AverageRating.Should().Be(4);
        // One star … five stars.
        shopfront.Rating.Counts.Should().Equal(0, 0, 1, 1, 1);
    }

    [Test]
    public async Task AnUnratedAgencyHasNoAverageRatherThanZero()
    {
        var agencyId = await AddTestAgencyAsync();

        var shopfront = await SendAsync(new GetMarketplaceAgencyQuery(agencyId));

        shopfront!.Rating.ReviewCount.Should().Be(0);
        shopfront.Rating.AverageRating.Should().BeNull();
    }

    [Test]
    public async Task SearchResultsCarryTheSellingAgencysRating()
    {
        var agencyId = await AddTestAgencyAsync();
        await AddAsync(new Car { Matricule = "MK-RATED", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") });
        await AddReviewAsync(agencyId, 4);
        await AddReviewAsync(agencyId, 5);

        var result = await SendAsync(new SearchAvailableCarsQuery(Start, End));

        result.Items.Should().HaveCount(1);
        result.Items.First().AgencyRating.Should().Be(4.5);
        result.Items.First().AgencyReviewCount.Should().Be(2);
    }

    [Test]
    public async Task PublicReviewListIsNewestFirstAndCarriesTheSnapshottedLabels()
    {
        var (_, agencyId, renting) = await FinishedRentingAsync("rate6@local");

        await SendAsync(new CreateMyReviewCommand { RentingId = renting.Id, Rating = 5, Comment = "Great." });
        // The command stamps "now" from the TimeProvider, so the older review is
        // dated well before any clock the suite could be running against — not
        // relative to Start, which is a booking window in the future.
        await AddReviewAsync(agencyId, 2, submittedAt: new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var page = await SendAsync(new GetAgencyReviewsQuery(agencyId));

        page.TotalCount.Should().Be(2);
        page.Items.First().Rating.Should().Be(5); // newest first
        page.Items.First().AuthorName.Should().Be("Ann Bee");
        page.Items.First().Comment.Should().Be("Great.");
        page.Items.Last().Rating.Should().Be(2);
    }

    // ----------------------------------------------------------------- map ---

    [Test]
    public async Task MapGroupsAvailableCarsIntoOnePinPerPickupPlace()
    {
        await AddTestAgencyAsync();
        var branch = await GeocodedBranchAsync("Tunis Airport", latitude: 36.8510, longitude: 10.2272);

        await AddAsync(new Car
        {
            Matricule = "MK-M1", Status = CarStatus.Active,
            DailyRate = Money.Of(70m, "TND"), BranchId = branch.Id
        });
        await AddAsync(new Car
        {
            Matricule = "MK-M2", Status = CarStatus.Active,
            DailyRate = Money.Of(45m, "TND"), BranchId = branch.Id
        });
        // Branchless: listable, but there is nowhere to pin it.
        await AddAsync(new Car { Matricule = "MK-M3", Status = CarStatus.Active, DailyRate = Money.Of(10m, "TND") });

        var points = await SendAsync(new SearchCarsMapQuery(Start, End));

        points.Should().HaveCount(1);
        points[0].BranchId.Should().Be(branch.Id);
        points[0].CarCount.Should().Be(2);
        points[0].Latitude.Should().BeApproximately(36.8510, 0.0001);
        points[0].Longitude.Should().BeApproximately(10.2272, 0.0001);
        points[0].FromDailyRate!.Amount.Should().Be(45m);
        points[0].FromDailyRate!.Currency.Should().Be("TND");
    }

    [Test]
    public async Task MapExcludesPlacesOutsideTheViewport()
    {
        await AddTestAgencyAsync();
        var tunis = await GeocodedBranchAsync("Tunis", latitude: 36.85, longitude: 10.22);
        var djerba = await GeocodedBranchAsync("Djerba", latitude: 33.80, longitude: 10.99);

        await AddAsync(new Car
        {
            Matricule = "MK-V1", Status = CarStatus.Active,
            DailyRate = Money.Of(50m, "TND"), BranchId = tunis.Id
        });
        await AddAsync(new Car
        {
            Matricule = "MK-V2", Status = CarStatus.Active,
            DailyRate = Money.Of(50m, "TND"), BranchId = djerba.Id
        });

        // A box around Tunis only.
        var points = await SendAsync(new SearchCarsMapQuery(
            Start, End, South: 36.0, West: 9.5, North: 37.5, East: 11.0));

        points.Should().HaveCount(1);
        points[0].BranchId.Should().Be(tunis.Id);
    }

    [Test]
    public async Task MapAndListAgreeOnWhatIsAvailable()
    {
        await AddTestAgencyAsync();
        var branch = await GeocodedBranchAsync("Tunis", latitude: 36.85, longitude: 10.22);

        var free = new Car
        {
            Matricule = "MK-FREE", Status = CarStatus.Active,
            DailyRate = Money.Of(50m, "TND"), BranchId = branch.Id
        };
        await AddAsync(free);

        var booked = new Car
        {
            Matricule = "MK-HELD", Status = CarStatus.Active,
            DailyRate = Money.Of(20m, "TND"), BranchId = branch.Id
        };
        await AddAsync(booked);
        await AddAsync(Reservation.Create(booked.Id, Start, End, price: null, expiresAt: Start.AddHours(-1)));

        var points = await SendAsync(new SearchCarsMapQuery(Start, End));

        // The held car neither counts towards the pin nor sets its "from" price,
        // which is the whole point of the map sharing the list's availability rule.
        points.Should().HaveCount(1);
        points[0].CarCount.Should().Be(1);
        points[0].FromDailyRate!.Amount.Should().Be(50m);
    }

    // --------------------------------------------------------------- setup ---

    // A customer with one finished rental at a fresh agency — the starting point
    // for every rating test. Leaves the caller signed in as that customer with
    // no tenant, the way a marketplace request actually arrives.
    private static async Task<(string CustomerId, int AgencyId, Renting Renting)> FinishedRentingAsync(
        string userName, RentingState state = RentingState.Done)
    {
        var customerId = await RunAsUserAsync(userName, "Customer1234!", new[] { Roles.Customer });
        var agencyId = await AddTestAgencyAsync();

        var car = new Car { Matricule = "MK-R", Status = CarStatus.Active, DailyRate = Money.Of(50m, "TND") };
        await AddAsync(car);

        var client = new Client { FirstName = "Ann", LastName = "Bee", MarketplaceUserId = customerId };
        await AddAsync(client);

        var renting = new Renting
        {
            CarId = car.Id,
            ClientId = client.Id,
            StartDate = Start,
            EndDate = End,
            RentingState = state,
            Price = Money.Of(150m, "TND"),
        };
        await AddAsync(renting);

        SetCurrentAgency(null); // a customer has no tenant of their own

        return (customerId, agencyId, renting);
    }

    // A review written straight to the table, for the cases that are about
    // aggregation rather than about the command that produces one.
    //
    // Every review hangs off a renting, and a renting is tenant data, so the
    // agency's tenant is pushed for the seeding and dropped again afterwards —
    // callers are marketplace visitors, who have none.
    private static async Task AddReviewAsync(int agencyId, int rating, DateTime? submittedAt = null)
    {
        SetCurrentAgency(agencyId);

        // No car: Renting.CarId is nullable, and which car it was does not
        // matter to an average.
        var renting = new Renting { RentingState = RentingState.Done };
        await AddAsync(renting);

        await AddAsync(new AgencyReview
        {
            AgencyId = agencyId,
            RentingId = renting.Id,
            Rating = rating,
            AuthorName = "Someone",
            SubmittedAt = submittedAt ?? Start,
        });

        SetCurrentAgency(null);
    }

    private static async Task<Branch> GeocodedBranchAsync(string name, double latitude, double longitude)
    {
        var country = new Country { Name = $"Mapland-{name}" };
        await AddAsync(country);

        var branch = new Branch
        {
            Name = name,
            CountryId = country.Id,
            Location = new Point(longitude, latitude) { SRID = 4326 },
        };
        await AddAsync(branch);

        return branch;
    }
}
