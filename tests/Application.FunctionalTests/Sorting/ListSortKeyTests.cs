using RemSolution.Application.Features.Car.Queries.GetCarsWithPaginationQuery;
using RemSolution.Application.Features.Client.Queries.GetClientsWithPaginationQuery;
using RemSolution.Application.Features.Credit.Queries.GetClientCreditsQuery;
using RemSolution.Application.Features.Credit.Queries.GetExpenseCreditsQuery;
using RemSolution.Application.Features.Expense.Queries.GetExpensesWithPaginationQuery;
using RemSolution.Application.Features.ModelCar.Queries.GetModelCarsWithPaginationQuery;
using RemSolution.Application.Features.Renting.Queries.GetRentingsWithPaginationQuery;
using RemSolution.Application.Features.Reservation.Queries.GetReservationsWithPaginationQuery;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using ExpenseEntity = RemSolution.Domain.Entities.Expense;
using ExpenseTypeEntity = RemSolution.Domain.Entities.ExpenseType;

namespace RemSolution.Application.FunctionalTests.Sorting;

using static Testing;

/// <summary>
/// The list screens sort server-side, and the column ids the Angular tables use
/// travel to the API as SortBy (see SortingExtensions). Every key below is one a
/// user can actually produce by clicking a header, so each one has to translate
/// to SQL — an untranslatable ORDER BY only fails when the column is clicked,
/// which is exactly the kind of break that reaches production. These tests run
/// every key in both directions, and pin the fallback contract: an unrecognised
/// key returns the query's default order instead of throwing.
/// </summary>
public class ListSortKeyTests : BaseTestFixture
{
    // Keys per screen, matching the matColumnDef ids in the templates.
    private static readonly string[] CarKeys =
        ["matricule", "model", "firstCirculationDate", "color", "power", "fuelType", "status", "dailyRate", "branch"];

    private static readonly string[] ClientKeys = ["name", "birthDate", "cin", "flagged"];

    private static readonly string[] ModelKeys = ["name", "brand"];

    private static readonly string[] ExpenseKeys = ["date", "car", "type", "amount", "paid", "outstanding"];

    private static readonly string[] RentingKeys = ["car", "client", "period", "state", "price", "endDate"];

    private static readonly string[] ReservationKeys = ["car", "client", "period", "status", "paid", "price", "expires"];

    private static readonly string[] ClientCreditKeys =
        ["name", "cin", "openRentings", "charged", "paid", "outstanding"];

    private static readonly string[] ExpenseCreditKeys = ["date", "car", "type", "amount", "paid", "outstanding"];

    [Test]
    public async Task EveryCarSortKeyTranslates()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await SeedFleetAsync();

        foreach (var key in CarKeys.Append("noSuchColumn"))
        {
            foreach (var descending in new[] { false, true })
            {
                var result = await SendAsync(new GetCarsWithPaginationQuery
                {
                    PageSize = 50, SortBy = key, SortDescending = descending
                });

                result.Items.Should().HaveCount(3, $"sorting cars by '{key}' must return every row");
            }
        }
    }

    [Test]
    public async Task CarsSortByTheRequestedColumnAndDirection()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await SeedFleetAsync();

        var ascending = await SendAsync(new GetCarsWithPaginationQuery { PageSize = 50, SortBy = "matricule" });
        ascending.Items.Select(c => c.Matricule).Should().Equal("AAA-1", "BBB-2", "CCC-3");

        var descending = await SendAsync(new GetCarsWithPaginationQuery
        {
            PageSize = 50, SortBy = "matricule", SortDescending = true
        });
        descending.Items.Select(c => c.Matricule).Should().Equal("CCC-3", "BBB-2", "AAA-1");

        // Numbers must order numerically, not as text: 9 before 10.
        var byPower = await SendAsync(new GetCarsWithPaginationQuery { PageSize = 50, SortBy = "power" });
        byPower.Items.Select(c => c.Power).Should().Equal(9, 10, 110);
    }

    [Test]
    public async Task AnUnknownCarSortKeyFallsBackToTheDefaultOrder()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await SeedFleetAsync();

        var unknown = await SendAsync(new GetCarsWithPaginationQuery { PageSize = 50, SortBy = "'; drop table Cars --" });

        // The default order is the matricule; nothing the caller sends can widen
        // the set of columns the query is willing to order by.
        unknown.Items.Select(c => c.Matricule).Should().Equal("AAA-1", "BBB-2", "CCC-3");
    }

    [Test]
    public async Task EveryClientAndModelSortKeyTranslates()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var brand = new Brand { Name = "Kia" };
        await AddAsync(brand);
        await AddAsync(new ModelCar { Name = "Picanto", BrandId = brand.Id });

        await AddAsync(new Client
        {
            FirstName = "Amel", LastName = "Zouari", CIN = "11111111",
            BirthDate = new DateTime(1990, 5, 4, 0, 0, 0, DateTimeKind.Utc)
        });
        await AddAsync(new Client { FirstName = "Brahim", LastName = "Ayari", CIN = "22222222", IsFlagged = true });

        foreach (var key in ClientKeys.Append("noSuchColumn"))
        {
            foreach (var descending in new[] { false, true })
            {
                var result = await SendAsync(new GetClientsWithPaginationQuery
                {
                    PageSize = 50, SortBy = key, SortDescending = descending
                });

                result.Items.Should().HaveCount(2, $"sorting clients by '{key}' must return every row");
            }
        }

        // Default order stays surname-first, as before sorting existed.
        var byName = await SendAsync(new GetClientsWithPaginationQuery { PageSize = 50 });
        byName.Items.Select(c => c.LastName).Should().Equal("Ayari", "Zouari");

        foreach (var key in ModelKeys.Append("noSuchColumn"))
        {
            var result = await SendAsync(new GetModelCarsWithPaginationQuery { PageSize = 50, SortBy = key });
            result.Items.Should().HaveCount(1, $"sorting models by '{key}' must return every row");
        }
    }

    [Test]
    public async Task EveryExpenseAndCreditSortKeyTranslates()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = new Car { Matricule = "EXP-9", Status = CarStatus.Active };
        await AddAsync(car);
        var type = new ExpenseTypeEntity { Name = "Tyres", IsActive = true };
        await AddAsync(type);

        // Two expenses with different debts, so the money columns have something
        // to order: 400 owing versus nothing owing.
        await AddAsync(new ExpenseEntity
        {
            CarId = car.Id, ExpenseTypeId = type.Id,
            ExpenseDate = new DateTime(2030, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            ExpenseAmount = Money.Of(500m, "TND"), PaidAmount = Money.Of(100m, "TND")
        });
        await AddAsync(new ExpenseEntity
        {
            CarId = car.Id, ExpenseTypeId = type.Id,
            ExpenseDate = new DateTime(2030, 2, 5, 0, 0, 0, DateTimeKind.Utc),
            ExpenseAmount = Money.Of(200m, "TND"), PaidAmount = Money.Of(200m, "TND")
        });

        foreach (var key in ExpenseKeys.Append("noSuchColumn"))
        {
            foreach (var descending in new[] { false, true })
            {
                var result = await SendAsync(new GetExpensesWithPaginationQuery
                {
                    PageSize = 50, SortBy = key, SortDescending = descending
                });

                result.Items.Should().HaveCount(2, $"sorting expenses by '{key}' must return every row");
            }
        }

        // Outstanding is amount − paid, computed in SQL: the 400 still owed
        // outranks the settled one.
        var byOutstanding = await SendAsync(new GetExpensesWithPaginationQuery
        {
            PageSize = 50, SortBy = "outstanding", SortDescending = true
        });
        byOutstanding.Items.First().Outstanding!.Amount.Should().Be(400m);

        foreach (var key in ExpenseCreditKeys.Append("noSuchColumn"))
        {
            foreach (var descending in new[] { false, true })
            {
                var result = await SendAsync(new GetExpenseCreditsQuery
                {
                    PageSize = 50, OnlyOutstanding = false, SortBy = key, SortDescending = descending
                });

                result.Items.Should().HaveCount(2, $"sorting expense credits by '{key}' must return every row");
            }
        }

        await AddAsync(new Client { FirstName = "Ines", LastName = "Bouzid", CIN = "33333333" });

        foreach (var key in ClientCreditKeys.Append("noSuchColumn"))
        {
            foreach (var descending in new[] { false, true })
            {
                var result = await SendAsync(new GetClientCreditsQuery
                {
                    PageSize = 50, OnlyOutstanding = false, SortBy = key, SortDescending = descending
                });

                result.Items.Should().HaveCount(1, $"sorting client credits by '{key}' must return every row");
            }
        }
    }

    [Test]
    public async Task EveryBookingSortKeyTranslates()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = new Car { Matricule = "BOOK-1", Status = CarStatus.Active };
        await AddAsync(car);
        var client = new Client { FirstName = "Nour", LastName = "Trabelsi", CIN = "44444444" };
        await AddAsync(client);

        await AddAsync(new Renting
        {
            CarId = car.Id, ClientId = client.Id,
            StartDate = new DateTime(2030, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2030, 4, 5, 0, 0, 0, DateTimeKind.Utc),
            Price = Money.Of(600m, "TND"), RentingState = RentingState.NotYet
        });

        foreach (var key in RentingKeys.Append("noSuchColumn"))
        {
            foreach (var descending in new[] { false, true })
            {
                var result = await SendAsync(new GetRentingsWithPaginationQuery
                {
                    PageSize = 50, SortBy = key, SortDescending = descending
                });

                result.Items.Should().HaveCount(1, $"sorting rentings by '{key}' must return every row");
            }
        }

        // Reservations are an aggregate built through their factory, so the table
        // is left empty here: the ORDER BY still has to translate and run.
        foreach (var key in ReservationKeys.Append("noSuchColumn"))
        {
            foreach (var descending in new[] { false, true })
            {
                var result = await SendAsync(new GetReservationsWithPaginationQuery
                {
                    PageSize = 50, SortBy = key, SortDescending = descending
                });

                result.Items.Should().BeEmpty();
            }
        }
    }

    // Three cars whose text, numeric and date columns all sort differently, so a
    // wrong ORDER BY shows up as a wrong sequence rather than passing by luck.
    private static async Task SeedFleetAsync()
    {
        var brand = new Brand { Name = "Peugeot" };
        await AddAsync(brand);

        var model = new ModelCar { Name = "208", BrandId = brand.Id };
        await AddAsync(model);

        await AddAsync(new Car
        {
            Matricule = "CCC-3", ModelId = model.Id, Color = "Amber", Power = 10,
            FuelType = FuelType.Diesel, Status = CarStatus.Active,
            DailyRate = Money.Of(90m, "TND"),
            FirstCirculationDate = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await AddAsync(new Car
        {
            Matricule = "AAA-1", ModelId = model.Id, Color = "Zinc", Power = 110,
            FuelType = FuelType.Gasoline, Status = CarStatus.Maintenance,
            DailyRate = Money.Of(120m, "TND"),
            FirstCirculationDate = new DateTime(2021, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        // No model, no rate, no fuel type: the nullable columns have to sort too.
        await AddAsync(new Car
        {
            Matricule = "BBB-2", Color = "Mint", Power = 9, Status = CarStatus.Active,
            FirstCirculationDate = new DateTime(2020, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
