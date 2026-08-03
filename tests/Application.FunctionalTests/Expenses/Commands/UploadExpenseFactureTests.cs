using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Expense.Commands.CreateExpenseCommand;
using RemSolution.Application.Features.Expense.Commands.UploadExpenseFactureCommand;
using RemSolution.Application.Features.Expense.Queries.GetExpenseByIdQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using ExpenseEntity = RemSolution.Domain.Entities.Expense;
using ExpenseTypeEntity = RemSolution.Domain.Entities.ExpenseType;

namespace RemSolution.Application.FunctionalTests.Expenses.Commands;

using static Testing;

// The supplier invoice attached to an expense — the FactureFileId that had been
// left dormant until the finance screen grew a way to upload it.
public class UploadExpenseFactureTests : BaseTestFixture
{
    private static readonly byte[] PdfBytes = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };

    // Distinct content, so a replacement does NOT dedup against PdfBytes.
    private static readonly byte[] OtherBytes = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x35, 0x01 };

    private static string StoredPath(string url) =>
        Path.Combine(UploadsRoot, url.Substring("/uploads/".Length).Replace('/', Path.DirectorySeparatorChar));

    private static UploadExpenseFactureCommand MakeUpload(int expenseId, byte[]? content = null)
    {
        var bytes = content ?? PdfBytes;
        return new()
        {
            ExpenseId = expenseId,
            FileName = "facture.pdf",
            ContentType = "application/pdf",
            Length = bytes.Length,
            Content = new MemoryStream(bytes)
        };
    }

    private async Task<int> BookedExpenseAsync(string matricule)
    {
        var car = new Car { Matricule = matricule, Status = CarStatus.Active };
        await AddAsync(car);

        var type = new ExpenseTypeEntity { Name = $"Garage {matricule}", IsActive = true };
        await AddAsync(type);

        return await SendAsync(new CreateExpenseCommand
        {
            CarId = car.Id, ExpenseTypeId = type.Id, Amount = 180m
        });
    }

    [Test]
    public async Task AttachesTheInvoiceToTheExpense()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var expenseId = await BookedExpenseAsync("FAC-1");

        var url = await SendAsync(MakeUpload(expenseId));

        url.Should().StartWith("/uploads/");
        url.Should().EndWith(".pdf");

        var expense = await FindAsync<ExpenseEntity>(expenseId);
        expense!.FactureFileId.Should().NotBeNull();

        var file = await FindAsync<StoredFile>(expense.FactureFileId!.Value);
        file!.Url.Should().Be(url);
        file.OriginalFileName.Should().Be("facture.pdf");
        file.DocumentType.Should().Be(DocumentType.ExpenseFacture);
        file.Size.Should().Be(PdfBytes.Length);

        File.Exists(StoredPath(url)).Should().BeTrue();
    }

    [Test]
    public async Task ReUploadingReplacesThePreviousInvoice()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var expenseId = await BookedExpenseAsync("FAC-2");

        var firstUrl = await SendAsync(MakeUpload(expenseId, PdfBytes));
        var secondUrl = await SendAsync(MakeUpload(expenseId, OtherBytes));

        secondUrl.Should().NotBe(firstUrl);

        var expense = await FindAsync<ExpenseEntity>(expenseId);
        var file = await FindAsync<StoredFile>(expense!.FactureFileId!.Value);
        file!.Url.Should().Be(secondUrl);

        (await CountAsync<StoredFile>(f => f.Url == firstUrl)).Should().Be(0);
        File.Exists(StoredPath(firstUrl)).Should().BeFalse();
        File.Exists(StoredPath(secondUrl)).Should().BeTrue();
    }

    [Test]
    public async Task TheInvoiceUrlIsReadBackWithTheExpense()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var expenseId = await BookedExpenseAsync("FAC-3");

        var url = await SendAsync(MakeUpload(expenseId));

        var dto = await SendAsync(new GetExpenseByIdQuery(expenseId));
        dto!.FactureFileUrl.Should().Be(url);
        dto.FactureFileName.Should().Be("facture.pdf");
    }

    [Test]
    public async Task RejectsADisallowedContentType()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var expenseId = await BookedExpenseAsync("FAC-4");

        var command = MakeUpload(expenseId) with
        {
            FileName = "malware.exe",
            ContentType = "application/octet-stream"
        };

        await FluentActions.Invoking(() => SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task StaffWithoutTheUpdatePermissionIsDenied()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var expenseId = await BookedExpenseAsync("FAC-5");

        await RunAsAgencyStaffAsync(Permissions.ExpenseRead);

        await FluentActions.Invoking(() => SendAsync(MakeUpload(expenseId)))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task AnExpenseOfAnotherAgencyIsNotFound()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var expenseId = await BookedExpenseAsync("FAC-6");

        await AddTestAgencyAsync(); // second tenant

        await FluentActions.Invoking(() => SendAsync(MakeUpload(expenseId)))
            .Should().ThrowAsync<NotFoundException>();
    }
}
