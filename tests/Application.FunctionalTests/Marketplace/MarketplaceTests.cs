using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Marketplace.Commands.CancelMyReservationCommand;
using RemSolution.Application.Features.Marketplace.Commands.CreateCustomerReservationCommand;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMarketplaceAgencyQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMarketplaceDestinationsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetMyReservationsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.GetShowcaseCarsQuery;
using RemSolution.Application.Features.MarketplaceSearch.Queries.SearchAvailableCarsQuery;
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
}
