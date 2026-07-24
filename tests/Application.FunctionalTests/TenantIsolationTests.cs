using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Common.Tenancy;
using RemSolution.Application.Features.Car.Commands.CreateCarCommand;
using RemSolution.Application.Features.Car.Queries.GetCarByIdQuery;
using RemSolution.Application.Features.Car.Queries.GetCarsWithPaginationQuery;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests;

using static Testing;

public class TenantIsolationTests : BaseTestFixture
{
    // A platform administrator holds neither the agency role nor permission
    // claims, so the read is denied unless an impersonation scope is active.
    [Test]
    public async Task PlatformAdmin_CannotReadTenantData_WithoutImpersonation()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyA = await AddTestAgencyAsync();
        await SeedCarAsync(agencyA, "IMP-001");

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(agencyA);

        await FluentActions.Invoking(() =>
            SendAsync(new GetCarsWithPaginationQuery { PageNumber = 1, PageSize = 10 }))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task PlatformAdmin_CanReadAgencyData_WhileImpersonating()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyA = await AddTestAgencyAsync();
        await SeedCarAsync(agencyA, "IMP-002");

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(agencyA);

        using (ImpersonationScope.Begin())
        {
            var page = await SendAsync(new GetCarsWithPaginationQuery { PageNumber = 1, PageSize = 10 });
            page.TotalCount.Should().Be(1);
        }
    }

    // The bypass only covers read permissions: a write stays forbidden even
    // while impersonating.
    [Test]
    public async Task PlatformAdmin_CannotWriteTenantData_WhileImpersonating()
    {
        await RunAsAgencyAdministratorAsync();
        var agencyA = await AddTestAgencyAsync();

        SetCurrentAgency(agencyA);
        var brand = new Brand { Name = "Tesla" };
        await AddAsync(brand);
        var model = new ModelCar { Name = "Model X", BrandId = brand.Id };
        await AddAsync(model);

        await RunAsPlatformAdministratorAsync();
        SetCurrentAgency(agencyA);

        using (ImpersonationScope.Begin())
        {
            await FluentActions.Invoking(() =>
                SendAsync(new CreateCarCommand
                {
                    Matricule = "IMP-003",
                    ModelId = model.Id,
                    Color = "Red",
                    FirstCirculationDate = DateTime.UtcNow
                }))
                .Should().ThrowAsync<ForbiddenAccessException>();
        }
    }

    private static async Task SeedCarAsync(int agencyId, string matricule)
    {
        SetCurrentAgency(agencyId);

        var brand = new Brand { Name = "Tesla" };
        await AddAsync(brand);

        var model = new ModelCar { Name = "Model S", BrandId = brand.Id };
        await AddAsync(model);

        await SendAsync(new CreateCarCommand
        {
            Matricule = matricule,
            ModelId = model.Id,
            Color = "Black",
            FirstCirculationDate = DateTime.UtcNow
        });
    }

    [Test]
    public async Task ShouldNotSeeAnotherAgencysData()
    {
        await RunAsAgencyAdministratorAsync();

        // Agency A creates a car.
        var agencyA = await AddTestAgencyAsync();

        var brand = new Brand { Name = "Tesla" };
        await AddAsync(brand);

        var model = new ModelCar { Name = "Model S", BrandId = brand.Id };
        await AddAsync(model);

        var carId = await SendAsync(new CreateCarCommand
        {
            Matricule = "TEN-001",
            ModelId = model.Id,
            Color = "Black",
            FirstCirculationDate = DateTime.UtcNow
        });

        var car = await FindAsync<Car>(carId);
        car!.AgencyId.Should().Be(agencyA);

        // Switch tenant to agency B: A's car must be invisible everywhere.
        await AddTestAgencyAsync();

        (await FindAsync<Car>(carId)).Should().BeNull();
        (await CountAsync<Car>()).Should().Be(0);

        var page = await SendAsync(new GetCarsWithPaginationQuery { PageNumber = 1, PageSize = 10 });
        page.TotalCount.Should().Be(0);

        await FluentActions.Invoking(() =>
            SendAsync(new GetCarByIdQuery(carId))).Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ShouldNotMoveARowToAnotherAgencyOnUpdate()
    {
        await RunAsAgencyAdministratorAsync();

        var agencyA = await AddTestAgencyAsync();
        var agencyB = await AddTestAgencyAsync();

        SetCurrentAgency(agencyA);

        var brand = new Brand { Name = "Tesla" };
        await AddAsync(brand);

        var model = new ModelCar { Name = "Model Y", BrandId = brand.Id };
        await AddAsync(model);

        var carId = await SendAsync(new CreateCarCommand
        {
            Matricule = "TEN-003",
            ModelId = model.Id,
            Color = "Gray",
            FirstCirculationDate = DateTime.UtcNow
        });

        // Attempt to smuggle the car into agency B through an update.
        var car = await FindAsync<Car>(carId);
        car!.AgencyId = agencyB;
        car.Color = "Green";

        await FluentActions.Invoking(() =>
            UpdateAsync(car)).Should().ThrowAsync<ForbiddenAccessException>();

        var unchanged = await FindAsync<Car>(carId);

        unchanged!.AgencyId.Should().Be(agencyA);
        unchanged.Color.Should().Be("Gray");
    }

    [Test]
    public async Task ShouldStampInsertsWithCurrentTenant()
    {
        await RunAsAgencyAdministratorAsync();

        await AddTestAgencyAsync();
        var agencyB = await AddTestAgencyAsync();

        var brand = new Brand { Name = "Tesla" };
        await AddAsync(brand);

        var model = new ModelCar { Name = "Model 3", BrandId = brand.Id };
        await AddAsync(model);

        var carId = await SendAsync(new CreateCarCommand
        {
            Matricule = "TEN-002",
            ModelId = model.Id,
            Color = "White",
            FirstCirculationDate = DateTime.UtcNow
        });

        var car = await FindAsync<Car>(carId);

        car!.AgencyId.Should().Be(agencyB);
    }
}
