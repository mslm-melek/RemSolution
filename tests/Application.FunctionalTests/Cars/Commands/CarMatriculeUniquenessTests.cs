using RemSolution.Application.Features.Car.Commands.CreateCarCommand;
using RemSolution.Application.Features.Car.Commands.DeleteCarCommand;
using RemSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace RemSolution.Application.FunctionalTests.Cars.Commands;

using static Testing;

// Verifies the filtered unique index IX_Cars_AgencyId_Matricule (WHERE
// IsDeleted = 0): a matricule is unique among live cars, but archiving frees it.
public class CarMatriculeUniquenessTests : BaseTestFixture
{
    [Test]
    public async Task RejectsADuplicateMatriculeAmongLiveCars()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var modelId = await SeedModelAsync();

        await SendAsync(NewCar("DUP-1", modelId));

        var second = async () => await SendAsync(NewCar("DUP-1", modelId));

        await second.Should().ThrowAsync<DbUpdateException>();
    }

    [Test]
    public async Task AllowsReusingAnArchivedCarsMatricule()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var modelId = await SeedModelAsync();

        var firstId = await SendAsync(NewCar("REUSE-1", modelId));
        await SendAsync(new DeleteCarCommand(firstId)); // archive

        // Same plate again — permitted because the archived row is filtered out.
        var reuse = async () => await SendAsync(NewCar("REUSE-1", modelId));

        await reuse.Should().NotThrowAsync();

        // One live car holds the plate; the archived one still exists (hidden).
        (await CountAsync<Car>(c => c.Matricule == "REUSE-1")).Should().Be(1);
        (await FindIgnoringFiltersAsync<Car>(c => c.Id == firstId))!.IsDeleted.Should().BeTrue();
    }

    private static async Task<int> SeedModelAsync()
    {
        var brand = new Brand { Name = "Peugeot" };
        await AddAsync(brand);
        var model = new ModelCar { Name = "208", BrandId = brand.Id };
        await AddAsync(model);
        return model.Id;
    }

    private static CreateCarCommand NewCar(string matricule, int modelId) => new()
    {
        Matricule = matricule,
        ModelId = modelId,
        FirstCirculationDate = DateTime.UtcNow
    };
}
