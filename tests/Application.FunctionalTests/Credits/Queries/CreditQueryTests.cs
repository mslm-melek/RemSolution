using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Credit.Queries.GetClientCreditsByIdsQuery;
using RemSolution.Application.Features.Credit.Queries.GetClientCreditsQuery;
using RemSolution.Application.Features.Credit.Queries.GetCreditsSummaryQuery;
using RemSolution.Application.Features.Credit.Queries.GetExpenseCreditsQuery;
using RemSolution.Application.Features.Expense.Commands.CreateExpenseCommand;
using RemSolution.Application.Features.Payment.Commands.CreatePaymentCommand;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using RemSolution.Domain.ValueObjects;
using ExpenseTypeEntity = RemSolution.Domain.Entities.ExpenseType;

namespace RemSolution.Application.FunctionalTests.Credits.Queries;

using static Testing;

public class CreditQueryTests : BaseTestFixture
{
    private static readonly DateTime Start = new(2030, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2030, 6, 4, 0, 0, 0, DateTimeKind.Utc);

    // A client charged `price` on a finished-state renting, having paid `paid`.
    private async Task<int> ClientWithRentingAsync(string name, decimal price, decimal paid)
    {
        var car = new Car { Matricule = $"CR-{name}", Status = CarStatus.Active };
        await AddAsync(car);

        var client = new Client { FirstName = name, LastName = "Debtor" };
        await AddAsync(client);

        await AddAsync(new Renting
        {
            CarId = car.Id,
            ClientId = client.Id,
            StartDate = Start,
            EndDate = End,
            RentingState = RentingState.InProgress,
            Price = Money.Of(price, "TND")
        });

        if (paid > 0)
        {
            await SendAsync(new CreatePaymentCommand { ClientId = client.Id, Amount = paid });
        }

        return client.Id;
    }

    private async Task<int> BookExpenseAsync(string matricule, decimal amount, decimal paid)
    {
        var car = new Car { Matricule = matricule, Status = CarStatus.Active };
        await AddAsync(car);

        var type = new ExpenseTypeEntity { Name = $"Type {matricule}", IsActive = true };
        await AddAsync(type);

        return await SendAsync(new CreateExpenseCommand
        {
            CarId = car.Id, ExpenseTypeId = type.Id, Amount = amount, PaidAmount = paid
        });
    }

    [Test]
    public async Task ClientCreditsListOnlyDebtorsByDefault()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await ClientWithRentingAsync("Owing", price: 300m, paid: 100m);
        await ClientWithRentingAsync("Settled", price: 200m, paid: 200m);

        var result = await SendAsync(new GetClientCreditsQuery());

        result.TotalCount.Should().Be(1);
        var row = result.Items.Single();
        row.ClientName.Should().Be("Owing Debtor");
        row.Charged!.Amount.Should().Be(300m);
        row.Paid!.Amount.Should().Be(100m);
        row.Outstanding!.Amount.Should().Be(200m);
        row.OpenRentingCount.Should().Be(1);
    }

    [Test]
    public async Task ClientCreditsCanListEveryClient()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await ClientWithRentingAsync("Owing", price: 300m, paid: 100m);
        await ClientWithRentingAsync("Settled", price: 200m, paid: 200m);

        var result = await SendAsync(new GetClientCreditsQuery(OnlyOutstanding: false));

        result.TotalCount.Should().Be(2);
    }

    [Test]
    public async Task ClientCreditsCanBeSearchedByName()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await ClientWithRentingAsync("Findme", price: 300m, paid: 0m);
        await ClientWithRentingAsync("Other", price: 300m, paid: 0m);

        var result = await SendAsync(new GetClientCreditsQuery(Search: "Findme"));

        result.TotalCount.Should().Be(1);
        result.Items.Single().ClientName.Should().Be("Findme Debtor");
    }

    [Test]
    public async Task CancelledRentingsAreNotCharged()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var car = new Car { Matricule = "CR-CANCEL", Status = CarStatus.Active };
        await AddAsync(car);
        var client = new Client { FirstName = "No", LastName = "Charge" };
        await AddAsync(client);
        await AddAsync(new Renting
        {
            CarId = car.Id, ClientId = client.Id, StartDate = Start, EndDate = End,
            RentingState = RentingState.Cancelled, Price = Money.Of(500m, "TND")
        });

        var result = await SendAsync(new GetClientCreditsQuery());

        result.TotalCount.Should().Be(0);
    }

    [Test]
    public async Task ExpenseCreditsListOnlyUnsettledByDefault()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await BookExpenseAsync("CR-EXP-1", amount: 400m, paid: 150m);
        await BookExpenseAsync("CR-EXP-2", amount: 100m, paid: 100m);

        var result = await SendAsync(new GetExpenseCreditsQuery());

        result.TotalCount.Should().Be(1);
        var row = result.Items.Single();
        row.Amount!.Amount.Should().Be(400m);
        row.Paid!.Amount.Should().Be(150m);
        row.Outstanding!.Amount.Should().Be(250m);
    }

    [Test]
    public async Task SummaryNetsBothSidesAndIgnoresClientsInCredit()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        // Owed to the agency: 200. A fully settled client adds nothing.
        await ClientWithRentingAsync("Owing", price: 300m, paid: 100m);
        await ClientWithRentingAsync("Settled", price: 200m, paid: 200m);

        // Owed by the agency: 250.
        await BookExpenseAsync("CR-SUM-1", amount: 400m, paid: 150m);
        await BookExpenseAsync("CR-SUM-2", amount: 100m, paid: 100m);

        var summary = await SendAsync(new GetCreditsSummaryQuery());

        summary.Currency.Should().Be("TND");
        summary.ClientsOutstanding!.Amount.Should().Be(200m);
        summary.ClientsInDebtCount.Should().Be(1);
        summary.ClientsCharged!.Amount.Should().Be(500m);
        summary.ClientsPaid!.Amount.Should().Be(300m);
        summary.ExpensesOutstanding!.Amount.Should().Be(250m);
        summary.ExpensesTotal!.Amount.Should().Be(500m);
        summary.ExpensesPaid!.Amount.Should().Be(250m);
        summary.UnpaidExpenseCount.Should().Be(1);
        summary.Net!.Amount.Should().Be(-50m); // 200 owed in, 250 owed out
    }

    [Test]
    public async Task SummaryTotalsReconcileWithTheClientRows()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        await ClientWithRentingAsync("A", price: 300m, paid: 100m);
        await ClientWithRentingAsync("B", price: 150m, paid: 0m);

        var summary = await SendAsync(new GetCreditsSummaryQuery());
        var rows = await SendAsync(new GetClientCreditsQuery(PageSize: 50));

        rows.Items.Sum(r => r.Outstanding!.Amount)
            .Should().Be(summary.ClientsOutstanding!.Amount);
    }

    [Test]
    public async Task CreditsAreScopedToTheCurrentAgency()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        await ClientWithRentingAsync("Hidden", price: 900m, paid: 0m);

        await AddTestAgencyAsync(); // second tenant

        var result = await SendAsync(new GetClientCreditsQuery());
        result.TotalCount.Should().Be(0);

        var summary = await SendAsync(new GetCreditsSummaryQuery());
        summary.ClientsOutstanding!.Amount.Should().Be(0m);
    }

    // The payable tab replaced the standalone expense list, so it offers the same
    // car/type narrowing that list did.
    [Test]
    public async Task ExpenseCreditsFilterByCarAndByType()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var garage = new ExpenseTypeEntity { Name = "Garage", IsActive = true };
        var fuel = new ExpenseTypeEntity { Name = "Fuel", IsActive = true };
        await AddAsync(garage);
        await AddAsync(fuel);

        var carA = new Car { Matricule = "CR-FIL-A", Status = CarStatus.Active };
        var carB = new Car { Matricule = "CR-FIL-B", Status = CarStatus.Active };
        await AddAsync(carA);
        await AddAsync(carB);

        // Car A owes on both types; car B owes on fuel only.
        await SendAsync(new CreateExpenseCommand { CarId = carA.Id, ExpenseTypeId = garage.Id, Amount = 300m });
        await SendAsync(new CreateExpenseCommand { CarId = carA.Id, ExpenseTypeId = fuel.Id, Amount = 90m });
        await SendAsync(new CreateExpenseCommand { CarId = carB.Id, ExpenseTypeId = fuel.Id, Amount = 60m });

        (await SendAsync(new GetExpenseCreditsQuery())).TotalCount.Should().Be(3);
        (await SendAsync(new GetExpenseCreditsQuery(CarId: carA.Id))).TotalCount.Should().Be(2);
        (await SendAsync(new GetExpenseCreditsQuery(ExpenseTypeId: fuel.Id))).TotalCount.Should().Be(2);

        var both = await SendAsync(new GetExpenseCreditsQuery(CarId: carA.Id, ExpenseTypeId: garage.Id));
        both.TotalCount.Should().Be(1);
        var row = both.Items.Single();
        row.ExpenseTypeId.Should().Be(garage.Id);
        row.Amount!.Amount.Should().Be(300m);
        // Nothing has been attached, so the invoice reads as absent rather than
        // as an empty string.
        row.FactureFileUrl.Should().BeNull();
    }

    [Test]
    public async Task StaffWithoutTheCreditPermissionIsDenied()
    {
        // Payment.Read alone must not unlock the debt overview.
        await RunAsAgencyStaffAsync(Permissions.PaymentRead);
        await AddTestAgencyAsync();

        await FluentActions.Invoking(() => SendAsync(new GetClientCreditsQuery()))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    // The client list asks for the debt of the page it is showing. Settled clients
    // must come back at zero, or a caller matching rows by id could not tell
    // "owes nothing" from "not answered".
    [Test]
    public async Task ClientCreditsByIdsAnswerForEveryIdAsked()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var owing = await ClientWithRentingAsync("Owing", price: 300m, paid: 100m);
        var settled = await ClientWithRentingAsync("Settled", price: 200m, paid: 200m);

        var rows = await SendAsync(new GetClientCreditsByIdsQuery(new[] { owing, settled }));

        rows.Should().HaveCount(2);
        rows.Single(r => r.ClientId == owing).Outstanding!.Amount.Should().Be(200m);
        rows.Single(r => r.ClientId == settled).Outstanding!.Amount.Should().Be(0m);
    }

    [Test]
    public async Task ClientCreditsByIdsAgreeWithTheCreditsList()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var id = await ClientWithRentingAsync("Owing", price: 300m, paid: 100m);

        var listed = (await SendAsync(new GetClientCreditsQuery())).Items.Single();
        var byId = (await SendAsync(new GetClientCreditsByIdsQuery(new[] { id }))).Single();

        byId.Charged!.Amount.Should().Be(listed.Charged!.Amount);
        byId.Paid!.Amount.Should().Be(listed.Paid!.Amount);
        byId.Outstanding!.Amount.Should().Be(listed.Outstanding!.Amount);
        byId.OpenRentingCount.Should().Be(listed.OpenRentingCount);
    }

    [Test]
    public async Task ClientCreditsByIdsAreScopedToTheCurrentAgency()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var hidden = await ClientWithRentingAsync("Hidden", price: 900m, paid: 0m);

        await AddTestAgencyAsync(); // second tenant

        var rows = await SendAsync(new GetClientCreditsByIdsQuery(new[] { hidden }));

        rows.Should().BeEmpty();
    }

    [Test]
    public async Task ClientCreditsByIdsWithoutIdsAsksTheDatabaseNothing()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        (await SendAsync(new GetClientCreditsByIdsQuery())).Should().BeEmpty();
        (await SendAsync(new GetClientCreditsByIdsQuery(Array.Empty<int>()))).Should().BeEmpty();
    }
}
