using RemSolution.Application.Features.Car.Queries.GetCarFacetsQuery;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.FunctionalTests.Cars.Queries;

using static Testing;

/// <summary>
/// The counts beside the fleet list's filters. What matters here is the rule that
/// makes the rail usable: a facet leaves out its OWN filter, so picking one status
/// does not flatten every other status to zero.
/// </summary>
public class GetCarFacetsQueryTests : BaseTestFixture
{
    [Test]
    public async Task ShouldCountTheFleetByStatusAndCustody()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var held = new Car { Matricule = "F-OUT", Status = CarStatus.Active };
        await AddAsync(held);
        await AddAsync(new Car { Matricule = "F-IN", Status = CarStatus.Active });
        await AddAsync(new Car { Matricule = "F-GARAGE", Status = CarStatus.Maintenance });

        await AddAsync(new Renting
        {
            CarId = held.Id,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            RentingState = RentingState.InProgress
        });

        var facets = await SendAsync(new GetCarFacetsQuery());

        facets.Total.Should().Be(3);
        facets.Statuses.Single(f => f.Status == CarStatus.Active).Count.Should().Be(2);
        facets.Statuses.Single(f => f.Status == CarStatus.Maintenance).Count.Should().Be(1);
        facets.OnRent.Should().Be(1);
        facets.InYard.Should().Be(2);
    }

    // The rule the rail depends on: the status counts ignore the status filter, so
    // they still say what clicking another one would show.
    [Test]
    public async Task ShouldCountEachDimensionWithoutItsOwnFilter()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await AddAsync(new Car { Matricule = "D-A1", Status = CarStatus.Active });
        await AddAsync(new Car { Matricule = "D-A2", Status = CarStatus.Active });
        await AddAsync(new Car { Matricule = "D-M1", Status = CarStatus.Maintenance });

        var facets = await SendAsync(new GetCarFacetsQuery(Status: CarStatus.Active));

        // The list itself is narrowed…
        facets.Total.Should().Be(2);
        // …while the rail still offers the way out.
        facets.Statuses.Single(f => f.Status == CarStatus.Maintenance).Count.Should().Be(1);
    }

    // Every other filter DOES apply, which is what makes a count a promise about
    // the rows that would appear.
    [Test]
    public async Task ShouldCountWithinTheOtherFilters()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var country = new Country { Name = "Facetland" };
        await AddAsync(country);
        var airport = new Branch { Name = "Airport", CountryId = country.Id };
        var city = new Branch { Name = "City", CountryId = country.Id };
        await AddAsync(airport);
        await AddAsync(city);

        await AddAsync(new Car { Matricule = "FB-1", Status = CarStatus.Active, BranchId = airport.Id });
        await AddAsync(new Car { Matricule = "FB-2", Status = CarStatus.Maintenance, BranchId = airport.Id });
        await AddAsync(new Car { Matricule = "FB-3", Status = CarStatus.Active, BranchId = city.Id });

        var facets = await SendAsync(new GetCarFacetsQuery(BranchId: airport.Id));

        facets.Total.Should().Be(2);
        // Statuses are counted inside the chosen branch.
        facets.Statuses.Single(f => f.Status == CarStatus.Active).Count.Should().Be(1);
        // Branches are counted as if no branch were chosen — both are still offered.
        facets.Branches.Should().HaveCount(2);
        facets.Branches.Single(f => f.Id == city.Id).Count.Should().Be(1);
        facets.Branches.Single(f => f.Id == airport.Id).Name.Should().Be("Airport");
    }

    [Test]
    public async Task ShouldCountBrandsThroughTheModelAndCarsWithoutOne()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var brand = new Brand { Name = "Peugeot" };
        await AddAsync(brand);
        var model = new ModelCar { Name = "208", BrandId = brand.Id };
        await AddAsync(model);

        await AddAsync(new Car { Matricule = "FBR-1", Status = CarStatus.Active, ModelId = model.Id });
        await AddAsync(new Car { Matricule = "FBR-2", Status = CarStatus.Active, ModelId = model.Id });
        // No model, so no brand: counted, and in the bucket with no id.
        await AddAsync(new Car { Matricule = "FBR-NONE", Status = CarStatus.Active });

        var facets = await SendAsync(new GetCarFacetsQuery());

        facets.Brands.Single(f => f.Id == brand.Id).Count.Should().Be(2);
        facets.Brands.Single(f => f.Id == brand.Id).Name.Should().Be("Peugeot");
        facets.Brands.Single(f => f.Id == null).Count.Should().Be(1);
    }

    [Test]
    public async Task ShouldCountWithinTheSearchTerm()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await AddAsync(new Car { Matricule = "SEEK-1", Status = CarStatus.Active });
        await AddAsync(new Car { Matricule = "OTHER-1", Status = CarStatus.Active });

        var facets = await SendAsync(new GetCarFacetsQuery(Search: "SEEK"));

        facets.Total.Should().Be(1);
        facets.Statuses.Single(f => f.Status == CarStatus.Active).Count.Should().Be(1);
        // The whole fleet, which is what "clear" would show.
        facets.Fleet.Should().Be(2);
    }
}
