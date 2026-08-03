using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Payment.Commands.CreatePaymentCommand;
using RemSolution.Application.Features.Payment.Commands.UploadPaymentProofCommand;
using RemSolution.Application.Features.Payment.Queries.GetPaymentsWithPaginationQuery;
using RemSolution.Domain.Constants;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using PaymentEntity = RemSolution.Domain.Entities.Payment;

namespace RemSolution.Application.FunctionalTests.Payments.Commands;

using static Testing;

// The proof kept against a payment entry: a receipt, a transfer slip, or the
// invoice behind it. Same StoredFile plumbing as the client documents, so these
// cover the wiring rather than re-testing hashing and dedup.
public class UploadPaymentProofTests : BaseTestFixture
{
    private static readonly byte[] PdfBytes = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };

    // Distinct content, so a replacement does NOT dedup against PdfBytes.
    private static readonly byte[] OtherBytes = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x35, 0x01 };

    // Maps a returned /uploads/... URL to its on-disk location under the
    // test-isolated storage root configured in CustomWebApplicationFactory.
    private static string StoredPath(string url) =>
        Path.Combine(UploadsRoot, url.Substring("/uploads/".Length).Replace('/', Path.DirectorySeparatorChar));

    private static UploadPaymentProofCommand MakeUpload(int paymentId, byte[]? content = null)
    {
        var bytes = content ?? PdfBytes;
        return new()
        {
            PaymentId = paymentId,
            FileName = "receipt.pdf",
            ContentType = "application/pdf",
            Length = bytes.Length,
            Content = new MemoryStream(bytes)
        };
    }

    // A standalone client payment — the simplest entry that can carry a proof.
    private async Task<int> ClientPaymentAsync()
    {
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Payments, Enabled = true });

        var client = new Client { FirstName = "Proof", LastName = "Client" };
        await AddAsync(client);

        return await SendAsync(new CreatePaymentCommand { ClientId = client.Id, Amount = 120m });
    }

    [Test]
    public async Task AttachesTheFileToTheEntryAndTagsItAsPaymentProof()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var paymentId = await ClientPaymentAsync();

        var url = await SendAsync(MakeUpload(paymentId));

        url.Should().StartWith("/uploads/");
        url.Should().EndWith(".pdf");

        var payment = await FindAsync<PaymentEntity>(paymentId);
        payment!.ProofFileId.Should().NotBeNull();

        var file = await FindAsync<StoredFile>(payment.ProofFileId!.Value);
        file!.Url.Should().Be(url);
        file.OriginalFileName.Should().Be("receipt.pdf");
        file.MimeType.Should().Be("application/pdf");
        file.Size.Should().Be(PdfBytes.Length);
        file.DocumentType.Should().Be(DocumentType.PaymentProof);
        file.Sha256.Should().MatchRegex("^[0-9a-f]{64}$");

        File.Exists(StoredPath(url)).Should().BeTrue();
    }

    [Test]
    public async Task ReUploadingReplacesThePreviousProof()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var paymentId = await ClientPaymentAsync();

        var firstUrl = await SendAsync(MakeUpload(paymentId, PdfBytes));
        var secondUrl = await SendAsync(MakeUpload(paymentId, OtherBytes));

        secondUrl.Should().NotBe(firstUrl);

        var payment = await FindAsync<PaymentEntity>(paymentId);
        var file = await FindAsync<StoredFile>(payment!.ProofFileId!.Value);
        file!.Url.Should().Be(secondUrl);

        // The replaced file's record and bytes are both gone.
        (await CountAsync<StoredFile>(f => f.Url == firstUrl)).Should().Be(0);
        File.Exists(StoredPath(firstUrl)).Should().BeFalse();
        File.Exists(StoredPath(secondUrl)).Should().BeTrue();
    }

    [Test]
    public async Task TheProofUrlIsListedWithTheEntry()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var paymentId = await ClientPaymentAsync();

        var url = await SendAsync(MakeUpload(paymentId));

        var listed = await SendAsync(new GetPaymentsWithPaginationQuery());
        var row = listed.Items.Single(p => p.Id == paymentId);
        row.ProofFileUrl.Should().Be(url);
        row.ProofFileName.Should().Be("receipt.pdf");
    }

    [Test]
    public async Task RejectsADisallowedContentType()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var paymentId = await ClientPaymentAsync();

        var command = MakeUpload(paymentId) with
        {
            FileName = "malware.exe",
            ContentType = "application/octet-stream"
        };

        await FluentActions.Invoking(() => SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task RejectsAFileOverTheSizeLimit()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var paymentId = await ClientPaymentAsync();

        var command = MakeUpload(paymentId) with { Length = 16 * 1024 * 1024 };

        await FluentActions.Invoking(() => SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task StaffWithoutTheUpdatePermissionIsDenied()
    {
        // Create the entry as the administrator, then come back as staff who may
        // record payments but not edit them.
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var paymentId = await ClientPaymentAsync();

        await RunAsAgencyStaffAsync(Permissions.PaymentRead, Permissions.PaymentCreate);

        await FluentActions.Invoking(() => SendAsync(MakeUpload(paymentId)))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task APaymentOfAnotherAgencyIsNotFound()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var paymentId = await ClientPaymentAsync();

        await AddTestAgencyAsync(); // second tenant
        await AddAsync(new AgencyFeature { Feature = FeatureFlags.Payments, Enabled = true });

        await FluentActions.Invoking(() => SendAsync(MakeUpload(paymentId)))
            .Should().ThrowAsync<NotFoundException>();
    }
}
