using RemSolution.Application.Features.Car.Commands.CreateCarCommand;
using RemSolution.Application.Features.Car.Queries.GetCarsWithPaginationQuery;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.FunctionalTests.Cars.Queries;

using static Testing;

public class GetCarsWithPaginationQueryTests : BaseTestFixture
{
    [Test]
    public async Task ShouldReturnPaginatedCars()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var brand = new Brand { Name = "Tesla" };
        await AddAsync(brand);

        var model = new ModelCar { Name = "Model S", BrandId = brand.Id };
        await AddAsync(model);
        await SendAsync(new CreateCarCommand { Matricule = "CAR-1", ModelId = model.Id, Color = "Black", FirstCirculationDate = DateTime.UtcNow });
        await SendAsync(new CreateCarCommand { Matricule = "CAR-2", ModelId = model.Id, Color = "White", FirstCirculationDate = DateTime.UtcNow });

        var query = new GetCarsWithPaginationQuery { PageNumber = 1, PageSize = 10 };

        var result = await SendAsync(query);

        result.Items.Should().HaveCountGreaterThan(0);
        result.TotalCount.Should().BeGreaterThan(1);
    }

    // The dashboard's fleet counts link into this list, so the filters below have
    // to select exactly the cars those counts measured.

    [Test]
    public async Task ShouldFilterByStatus()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await AddAsync(new Car { Matricule = "ACTIVE-1", Status = CarStatus.Active });
        await AddAsync(new Car { Matricule = "GARAGE-1", Status = CarStatus.Maintenance });

        var result = await SendAsync(new GetCarsWithPaginationQuery { Status = CarStatus.Active });

        result.TotalCount.Should().Be(1);
        result.Items.First().Matricule.Should().Be("ACTIVE-1");
    }

    [Test]
    public async Task ShouldFilterCarsThatAreOutOnAHire()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var onRent = new Car { Matricule = "OUT-1", Status = CarStatus.Active };
        await AddAsync(onRent);
        await AddAsync(new Car { Matricule = "IN-1", Status = CarStatus.Active });

        await AddAsync(new Renting
        {
            CarId = onRent.Id,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            RentingState = RentingState.InProgress
        });

        var out_ = await SendAsync(new GetCarsWithPaginationQuery { OnRent = true });

        out_.TotalCount.Should().Be(1);
        out_.Items.First().Matricule.Should().Be("OUT-1");

        var available = await SendAsync(new GetCarsWithPaginationQuery { OnRent = false });

        available.TotalCount.Should().Be(1);
        available.Items.First().Matricule.Should().Be("IN-1");
    }

    // The cars list shows custody on the row itself — status chip, and a return
    // action that needs the hire holding the car — so the row carries it.
    [Test]
    public async Task ShouldReportCustodyAndHistorySizeOnTheRow()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var client = new Client { FirstName = "Amina", LastName = "Ben Salah" };
        await AddAsync(client);

        var held = new Car { Matricule = "HELD-1", Status = CarStatus.Active };
        await AddAsync(held);
        var idle = new Car { Matricule = "IDLE-1", Status = CarStatus.Active };
        await AddAsync(idle);

        await AddAsync(new Renting
        {
            CarId = held.Id,
            ClientId = client.Id,
            StartDate = DateTime.UtcNow.AddDays(-2),
            EndDate = DateTime.UtcNow.AddDays(3),
            StartMileage = 42100,
            RentingState = RentingState.InProgress
        });

        // Finished: part of the car's history, but it is not holding the car.
        await AddAsync(new Renting
        {
            CarId = held.Id,
            ClientId = client.Id,
            StartDate = DateTime.UtcNow.AddDays(-20),
            EndDate = DateTime.UtcNow.AddDays(-15),
            RentingState = RentingState.Done
        });

        // Cancelled: never happened, so it is not history either.
        await AddAsync(new Renting
        {
            CarId = idle.Id,
            ClientId = client.Id,
            StartDate = DateTime.UtcNow.AddDays(-10),
            EndDate = DateTime.UtcNow.AddDays(-8),
            RentingState = RentingState.Cancelled
        });

        var result = await SendAsync(new GetCarsWithPaginationQuery());

        var out_ = result.Items.Single(c => c.Matricule == "HELD-1");
        out_.IsOnRent.Should().BeTrue();
        out_.RentingCount.Should().Be(2);
        out_.CurrentRenting.Should().NotBeNull();
        out_.CurrentRenting!.ClientName.Should().Be("Amina Ben Salah");
        out_.CurrentRenting.StartMileage.Should().Be(42100);

        var available = result.Items.Single(c => c.Matricule == "IDLE-1");
        available.IsOnRent.Should().BeFalse();
        available.CurrentRenting.Should().BeNull();
        available.RentingCount.Should().Be(0);
    }

    [Test]
    public async Task ShouldFilterByWhenTheCarWasAdded()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await AddAsync(new Car { Matricule = "NEW-1", Status = CarStatus.Active });

        var now = DateTimeOffset.UtcNow;

        var inWindow = await SendAsync(new GetCarsWithPaginationQuery
        {
            AddedFrom = now.AddDays(-1), AddedTo = now.AddDays(1)
        });

        inWindow.TotalCount.Should().Be(1);

        // The window is half-open, and this one is entirely in the future.
        var afterwards = await SendAsync(new GetCarsWithPaginationQuery
        {
            AddedFrom = now.AddDays(1), AddedTo = now.AddDays(2)
        });

        afterwards.TotalCount.Should().Be(0);
    }
}
