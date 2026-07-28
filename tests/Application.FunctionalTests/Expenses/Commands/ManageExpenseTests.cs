using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Expense.Commands.CreateExpenseCommand;
using RemSolution.Application.Features.Expense.Commands.DeleteExpenseCommand;
using RemSolution.Application.Features.Expense.Commands.RecordExpensePaymentCommand;
using RemSolution.Application.Features.Expense.Commands.UpdateExpenseCommand;
using RemSolution.Application.Features.Expense.Queries.GetExpensesWithPaginationQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using ExpenseEntity = RemSolution.Domain.Entities.Expense;
using ExpenseTypeEntity = RemSolution.Domain.Entities.ExpenseType;

namespace RemSolution.Application.FunctionalTests.Expenses.Commands;

using static Testing;

public class ManageExpenseTests : BaseTestFixture
{
    private static readonly DateTime BookedOn = new(2030, 3, 5, 0, 0, 0, DateTimeKind.Utc);

    private async Task<(int carId, int typeId)> FleetAndCatalogAsync(string matricule, bool typeActive = true)
    {
        var car = new Car { Matricule = matricule, Status = CarStatus.Active };
        await AddAsync(car);

        var type = new ExpenseTypeEntity { Name = $"Maintenance {matricule}", IsActive = typeActive };
        await AddAsync(type);

        return (car.Id, type.Id);
    }

    [Test]
    public async Task CreateBooksTheCostInTheAgencyCurrency()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (carId, typeId) = await FleetAndCatalogAsync("EXP-1");

        var id = await SendAsync(new CreateExpenseCommand
        {
            CarId = carId, ExpenseTypeId = typeId, ExpenseDate = BookedOn,
            Amount = 250m, Description = "Oil change"
        });

        var expense = await FindAsync<ExpenseEntity>(id);
        expense!.ExpenseAmount!.Amount.Should().Be(250m);
        expense.ExpenseAmount.Currency.Should().Be("TND");
        // Nothing settled unless the caller said so.
        expense.PaidAmount!.Amount.Should().Be(0m);
        expense.ExpenseDate.Should().Be(BookedOn);
    }

    [Test]
    public async Task CreateAcceptsAnAmountAlreadySettledOnTheSpot()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (carId, typeId) = await FleetAndCatalogAsync("EXP-2");

        var id = await SendAsync(new CreateExpenseCommand
        {
            CarId = carId, ExpenseTypeId = typeId, Amount = 100m, PaidAmount = 100m
        });

        var expense = await FindAsync<ExpenseEntity>(id);
        expense!.PaidAmount!.Amount.Should().Be(100m);
    }

    [Test]
    public async Task CreateRejectsSettlingMoreThanTheAmount()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (carId, typeId) = await FleetAndCatalogAsync("EXP-3");

        await FluentActions.Invoking(() => SendAsync(new CreateExpenseCommand
        {
            CarId = carId, ExpenseTypeId = typeId, Amount = 100m, PaidAmount = 120m
        })).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task CreateRejectsAnInactiveExpenseType()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (carId, typeId) = await FleetAndCatalogAsync("EXP-4", typeActive: false);

        await FluentActions.Invoking(() => SendAsync(new CreateExpenseCommand
        {
            CarId = carId, ExpenseTypeId = typeId, Amount = 50m
        })).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task CreateRejectsACarFromAnotherAgency()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (otherCarId, _) = await FleetAndCatalogAsync("EXP-OTHER");

        // Switches the current tenant: the car above is now invisible.
        await AddTestAgencyAsync();
        var (_, typeId) = await FleetAndCatalogAsync("EXP-MINE");

        await FluentActions.Invoking(() => SendAsync(new CreateExpenseCommand
        {
            CarId = otherCarId, ExpenseTypeId = typeId, Amount = 50m
        })).Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task SettlementsAccumulateAndAreCappedAtTheAmount()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (carId, typeId) = await FleetAndCatalogAsync("EXP-5");

        var id = await SendAsync(new CreateExpenseCommand
        {
            CarId = carId, ExpenseTypeId = typeId, Amount = 300m
        });

        await SendAsync(new RecordExpensePaymentCommand(id, 100m));
        await SendAsync(new RecordExpensePaymentCommand(id, 50m));

        var expense = await FindAsync<ExpenseEntity>(id);
        expense!.PaidAmount!.Amount.Should().Be(150m);

        // 150 + 200 > 300.
        await FluentActions.Invoking(() => SendAsync(new RecordExpensePaymentCommand(id, 200m)))
            .Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ASettlementCorrectionCannotGoBelowZero()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (carId, typeId) = await FleetAndCatalogAsync("EXP-6");

        var id = await SendAsync(new CreateExpenseCommand
        {
            CarId = carId, ExpenseTypeId = typeId, Amount = 300m, PaidAmount = 40m
        });

        await FluentActions.Invoking(() => SendAsync(new RecordExpensePaymentCommand(id, -60m)))
            .Should().ThrowAsync<ValidationException>();

        // A correction within what was settled is accepted.
        await SendAsync(new RecordExpensePaymentCommand(id, -40m));

        var expense = await FindAsync<ExpenseEntity>(id);
        expense!.PaidAmount!.Amount.Should().Be(0m);
    }

    [Test]
    public async Task UpdateCannotDropTheAmountBelowWhatIsSettled()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (carId, typeId) = await FleetAndCatalogAsync("EXP-7");

        var id = await SendAsync(new CreateExpenseCommand
        {
            CarId = carId, ExpenseTypeId = typeId, Amount = 300m, PaidAmount = 200m
        });

        await FluentActions.Invoking(() => SendAsync(new UpdateExpenseCommand
        {
            Id = id, CarId = carId, ExpenseTypeId = typeId, ExpenseDate = BookedOn, Amount = 150m
        })).Should().ThrowAsync<ValidationException>();

        // Raising it is fine, and leaves the settled total untouched.
        await SendAsync(new UpdateExpenseCommand
        {
            Id = id, CarId = carId, ExpenseTypeId = typeId, ExpenseDate = BookedOn, Amount = 400m
        });

        var expense = await FindAsync<ExpenseEntity>(id);
        expense!.ExpenseAmount!.Amount.Should().Be(400m);
        expense.PaidAmount!.Amount.Should().Be(200m);
    }

    [Test]
    public async Task DeleteRemovesTheExpense()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (carId, typeId) = await FleetAndCatalogAsync("EXP-8");

        var id = await SendAsync(new CreateExpenseCommand
        {
            CarId = carId, ExpenseTypeId = typeId, Amount = 60m
        });

        await SendAsync(new DeleteExpenseCommand(id));

        (await FindAsync<ExpenseEntity>(id)).Should().BeNull();
    }

    [Test]
    public async Task ListFiltersByCarAndByOutstandingBalance()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (carA, typeId) = await FleetAndCatalogAsync("EXP-A");

        var carB = new Car { Matricule = "EXP-B", Status = CarStatus.Active };
        await AddAsync(carB);

        // Car A: one fully settled, one still owing. Car B: one owing.
        await SendAsync(new CreateExpenseCommand
        {
            CarId = carA, ExpenseTypeId = typeId, Amount = 100m, PaidAmount = 100m
        });
        await SendAsync(new CreateExpenseCommand
        {
            CarId = carA, ExpenseTypeId = typeId, Amount = 100m, PaidAmount = 25m
        });
        await SendAsync(new CreateExpenseCommand
        {
            CarId = carB.Id, ExpenseTypeId = typeId, Amount = 80m
        });

        var all = await SendAsync(new GetExpensesWithPaginationQuery());
        all.TotalCount.Should().Be(3);

        var forCarA = await SendAsync(new GetExpensesWithPaginationQuery(CarId: carA));
        forCarA.TotalCount.Should().Be(2);

        var unpaid = await SendAsync(new GetExpensesWithPaginationQuery(OnlyUnpaid: true));
        unpaid.TotalCount.Should().Be(2);
        unpaid.Items.Should().OnlyContain(e => e.Outstanding!.Amount > 0);
    }

    [Test]
    public async Task StaffWithoutTheCreatePermissionIsDenied()
    {
        await RunAsAgencyStaffAsync(Permissions.ExpenseRead);
        await AddTestAgencyAsync();
        var (carId, typeId) = await FleetAndCatalogAsync("EXP-9");

        await FluentActions.Invoking(() => SendAsync(new CreateExpenseCommand
        {
            CarId = carId, ExpenseTypeId = typeId, Amount = 10m
        })).Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task ExpensesAreInvisibleToAnotherAgency()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var (carId, typeId) = await FleetAndCatalogAsync("EXP-10");
        await SendAsync(new CreateExpenseCommand
        {
            CarId = carId, ExpenseTypeId = typeId, Amount = 999m
        });

        await AddTestAgencyAsync(); // second tenant

        var result = await SendAsync(new GetExpensesWithPaginationQuery());
        result.TotalCount.Should().Be(0);
    }
}
