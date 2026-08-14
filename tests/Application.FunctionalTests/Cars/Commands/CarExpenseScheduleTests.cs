using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Car.Commands;
using RemSolution.Application.Features.Car.Commands.CreateCarCommand;
using RemSolution.Application.Features.Car.Commands.UpdateCarCommand;
using RemSolution.Application.Features.Car.Queries.GetCarExpenseSchedulesQuery;
using RemSolution.Domain.Entities;

namespace RemSolution.Application.FunctionalTests.Cars.Commands;

using static Testing;

// Per-car servicing intervals: "Vidange every 8 000 km on this van, 10 000 on
// the fleet". See CarExpenseSchedule.
public class CarExpenseScheduleTests : BaseTestFixture
{
    private static async Task<int> ModelAsync(string brandName)
    {
        var brand = new Brand { Name = brandName };
        await AddAsync(brand);

        var model = new ModelCar { Name = "Model", BrandId = brand.Id };
        await AddAsync(model);

        return model.Id;
    }

    private static async Task<int> TypeAsync(
        string name, bool withNotif = true, int? afterKilometer = 10_000, int? afterMonth = null)
    {
        var type = new ExpenseType
        {
            Name = name,
            IsActive = true,
            WithNotif = withNotif,
            AfterKilometer = afterKilometer,
            AfterMonth = afterMonth,
        };
        await AddAsync(type);
        return type.Id;
    }

    private static async Task<int> CarAsync(int modelId, string matricule) =>
        await SendAsync(new CreateCarCommand
        {
            Matricule = matricule,
            ModelId = modelId,
            FirstCirculationDate = DateTime.UtcNow,
        });

    [Test]
    public async Task ShouldLinkATypeToACarAsItIsCreated()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var modelId = await ModelAsync("Renault");
        var typeId = await TypeAsync("Vidange");

        var carId = await SendAsync(new CreateCarCommand
        {
            Matricule = "SCH-1",
            ModelId = modelId,
            FirstCirculationDate = DateTime.UtcNow,
            ExpenseSchedules = new List<CarExpenseScheduleInput>
            {
                new() { ExpenseTypeId = typeId, AfterKilometer = 8_000, LastDoneMileage = 80_000 }
            }
        });

        var rows = await AllIgnoringFiltersAsync<CarExpenseSchedule>();

        rows.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(
                new { CarId = carId, ExpenseTypeId = typeId, AfterKilometer = 8_000, LastDoneMileage = 80_000 },
                options => options.ExcludingMissingMembers());
    }

    [Test]
    public async Task ShouldGiveTwoCarsDifferentIntervalsForTheSameType()
    {
        // The whole point: one type, two cars, two schedules.
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var modelId = await ModelAsync("Peugeot");
        var typeId = await TypeAsync("Vidange");

        await SendAsync(new CreateCarCommand
        {
            Matricule = "SCH-8000",
            ModelId = modelId,
            FirstCirculationDate = DateTime.UtcNow,
            ExpenseSchedules = new List<CarExpenseScheduleInput>
            {
                new() { ExpenseTypeId = typeId, AfterKilometer = 8_000 }
            }
        });

        await SendAsync(new CreateCarCommand
        {
            Matricule = "SCH-10000",
            ModelId = modelId,
            FirstCirculationDate = DateTime.UtcNow,
            ExpenseSchedules = new List<CarExpenseScheduleInput>
            {
                new() { ExpenseTypeId = typeId, AfterKilometer = 10_000 }
            }
        });

        var rows = await AllIgnoringFiltersAsync<CarExpenseSchedule>();

        rows.Select(r => r.AfterKilometer).Should().BeEquivalentTo(new int?[] { 8_000, 10_000 });
    }

    [Test]
    public async Task ShouldReplaceTheWholeSetOnUpdate()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var modelId = await ModelAsync("Fiat");
        var vidange = await TypeAsync("Vidange");
        var pneus = await TypeAsync("Pneus", afterKilometer: 40_000);

        var carId = await SendAsync(new CreateCarCommand
        {
            Matricule = "SCH-2",
            ModelId = modelId,
            FirstCirculationDate = DateTime.UtcNow,
            ExpenseSchedules = new List<CarExpenseScheduleInput>
            {
                new() { ExpenseTypeId = vidange, AfterKilometer = 8_000 },
                new() { ExpenseTypeId = pneus, AfterKilometer = 30_000 }
            }
        });

        // The form submits the full set; dropping "Pneus" from it unlinks it.
        await SendAsync(new UpdateCarCommand
        {
            Id = carId,
            ExpenseSchedules = new List<CarExpenseScheduleInput>
            {
                new() { ExpenseTypeId = vidange, AfterKilometer = 9_000 }
            }
        });

        var rows = await AllIgnoringFiltersAsync<CarExpenseSchedule>();

        rows.Should().ContainSingle();
        rows[0].ExpenseTypeId.Should().Be(vidange);
        rows[0].AfterKilometer.Should().Be(9_000);
    }

    [Test]
    public async Task ShouldLeaveSchedulesAloneWhenTheUpdateOmitsThem()
    {
        // Partial editors send no schedules; that is not "clear them".
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var modelId = await ModelAsync("Dacia");
        var typeId = await TypeAsync("Vidange");

        var carId = await SendAsync(new CreateCarCommand
        {
            Matricule = "SCH-3",
            ModelId = modelId,
            FirstCirculationDate = DateTime.UtcNow,
            ExpenseSchedules = new List<CarExpenseScheduleInput>
            {
                new() { ExpenseTypeId = typeId, AfterKilometer = 8_000 }
            }
        });

        await SendAsync(new UpdateCarCommand { Id = carId, Color = "Blue" });

        (await AllIgnoringFiltersAsync<CarExpenseSchedule>()).Should().ContainSingle();
    }

    [Test]
    public async Task ShouldClearSchedulesWhenTheUpdateSendsAnEmptySet()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var modelId = await ModelAsync("Seat");
        var typeId = await TypeAsync("Vidange");

        var carId = await SendAsync(new CreateCarCommand
        {
            Matricule = "SCH-4",
            ModelId = modelId,
            FirstCirculationDate = DateTime.UtcNow,
            ExpenseSchedules = new List<CarExpenseScheduleInput>
            {
                new() { ExpenseTypeId = typeId, AfterKilometer = 8_000 }
            }
        });

        await SendAsync(new UpdateCarCommand
        {
            Id = carId,
            ExpenseSchedules = new List<CarExpenseScheduleInput>()
        });

        (await AllIgnoringFiltersAsync<CarExpenseSchedule>()).Should().BeEmpty();
    }

    [Test]
    public async Task ShouldNotStoreARowThatOverridesNothing()
    {
        // A row with nothing typed in means the same as no row.
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var modelId = await ModelAsync("Opel");
        var typeId = await TypeAsync("Vidange");

        await SendAsync(new CreateCarCommand
        {
            Matricule = "SCH-5",
            ModelId = modelId,
            FirstCirculationDate = DateTime.UtcNow,
            ExpenseSchedules = new List<CarExpenseScheduleInput>
            {
                new() { ExpenseTypeId = typeId }
            }
        });

        (await AllIgnoringFiltersAsync<CarExpenseSchedule>()).Should().BeEmpty();
    }

    [Test]
    public async Task ShouldRejectATypeThatDoesNotNotify()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var modelId = await ModelAsync("Skoda");
        var typeId = await TypeAsync("Lavage", withNotif: false, afterKilometer: null);

        var carId = await CarAsync(modelId, "SCH-6");

        var command = new UpdateCarCommand
        {
            Id = carId,
            ExpenseSchedules = new List<CarExpenseScheduleInput>
            {
                new() { ExpenseTypeId = typeId, AfterKilometer = 8_000 }
            }
        };

        await FluentActions.Invoking(() => SendAsync(command))
            .Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldReadBackEveryNotifiableTypeWithTheCarsFiguresMergedIn()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var modelId = await ModelAsync("Toyota");
        var vidange = await TypeAsync("Vidange");
        var assurance = await TypeAsync("Assurance", afterKilometer: null, afterMonth: 12);

        var carId = await SendAsync(new CreateCarCommand
        {
            Matricule = "SCH-7",
            ModelId = modelId,
            FirstCirculationDate = DateTime.UtcNow,
            Mileage = 84_000,
            ExpenseSchedules = new List<CarExpenseScheduleInput>
            {
                new() { ExpenseTypeId = vidange, AfterKilometer = 8_000, LastDoneMileage = 80_000 }
            }
        });

        var rows = await SendAsync(new GetCarExpenseSchedulesQuery(carId));

        rows.Should().HaveCount(2);

        var custom = rows.Single(r => r.ExpenseTypeId == vidange);
        custom.IsLinked.Should().BeTrue();
        custom.HasOwnInterval.Should().BeTrue();
        custom.AfterKilometer.Should().Be(8_000);
        custom.TypeAfterKilometer.Should().Be(10_000);
        custom.EffectiveAfterKilometer.Should().Be(8_000);
        // 80 000 when it was last done + this car's 8 000 = due at 88 000.
        custom.NextDueAtKilometers.Should().Be(88_000);
        custom.IsOverdue.Should().BeFalse();

        var untouched = rows.Single(r => r.ExpenseTypeId == assurance);
        untouched.IsLinked.Should().BeFalse();
        untouched.EffectiveAfterMonth.Should().Be(12);
        // Nothing booked and nothing declared: no date to count from.
        untouched.NextDueOn.Should().BeNull();
    }

    [Test]
    public async Task ShouldOfferTheTypesWithNoCarYet()
    {
        // The new-car form asks the same question before the car exists.
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await TypeAsync("Vidange");

        var rows = await SendAsync(new GetCarExpenseSchedulesQuery());

        rows.Should().ContainSingle()
            .Which.IsLinked.Should().BeFalse();
    }
}
