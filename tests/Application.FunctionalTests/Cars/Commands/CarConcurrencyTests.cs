using RemSolution.Application.Features.Car.Commands.CreateCarCommand;
using RemSolution.Application.Features.Car.Commands.UpdateCarCommand;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Application.FunctionalTests.Cars.Commands;

using static Testing;

public class CarConcurrencyTests : BaseTestFixture
{
    [Test]
    public async Task UpdateWithAStaleRowVersionShouldRaiseAConcurrencyConflict()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var brand = new Brand { Name = "Tesla" };
        await AddAsync(brand);
        var model = new ModelCar { Name = "Model 3", BrandId = brand.Id };
        await AddAsync(model);

        var carId = await SendAsync(new CreateCarCommand
        {
            Matricule = "CONC-1",
            ModelId = model.Id,
            FirstCirculationDate = DateTime.UtcNow
        });

        // The token every editor starts from.
        var initial = (await FindAsync<Car>(carId))!.RowVersion;

        // First editor saves against that token — succeeds and bumps the version.
        await SendAsync(new UpdateCarCommand
        {
            Id = carId,
            RowVersion = initial,
            ModelId = model.Id,
            Status = CarStatus.Active,
            FirstCirculationDate = DateTime.UtcNow,
            Color = "Blue"
        });

        // Second editor still holds the original (now stale) token → conflict.
        var staleUpdate = async () => await SendAsync(new UpdateCarCommand
        {
            Id = carId,
            RowVersion = initial,
            ModelId = model.Id,
            Status = CarStatus.Maintenance,
            FirstCirculationDate = DateTime.UtcNow,
            Color = "Red"
        });

        await staleUpdate.Should().ThrowAsync<DbUpdateConcurrencyException>();

        // The first editor's change stands; the stale write was rejected.
        (await FindAsync<Car>(carId))!.Color.Should().Be("Blue");
    }

    [Test]
    public async Task UpdateWithTheCurrentRowVersionShouldSucceed()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var brand = new Brand { Name = "Kia" };
        await AddAsync(brand);
        var model = new ModelCar { Name = "Ceed", BrandId = brand.Id };
        await AddAsync(model);

        var carId = await SendAsync(new CreateCarCommand
        {
            Matricule = "CONC-2",
            ModelId = model.Id,
            FirstCirculationDate = DateTime.UtcNow
        });

        var current = (await FindAsync<Car>(carId))!.RowVersion;

        var update = async () => await SendAsync(new UpdateCarCommand
        {
            Id = carId,
            RowVersion = current,
            ModelId = model.Id,
            Status = CarStatus.Active,
            FirstCirculationDate = DateTime.UtcNow,
            Color = "Green"
        });

        await update.Should().NotThrowAsync();
        (await FindAsync<Car>(carId))!.Color.Should().Be("Green");
    }
}
