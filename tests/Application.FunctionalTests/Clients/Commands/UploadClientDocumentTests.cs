using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Client.Commands.CreateClientCommand;
using RemSolution.Application.Features.Client.Commands.DeleteClientCommand;
using RemSolution.Application.Features.Client.Commands.UploadClientDocumentCommand;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.FunctionalTests.Clients.Commands;

using static Testing;

public class UploadClientDocumentTests : BaseTestFixture
{
    private static readonly byte[] PngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    // Distinct content, so an upload of these bytes does NOT dedup against PngBytes.
    private static readonly byte[] OtherBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0B, 0x01, 0x02 };

    // Maps a returned /uploads/... URL to its on-disk location under the
    // test-isolated storage root configured in CustomWebApplicationFactory.
    private static string StoredPath(string url) =>
        Path.Combine(UploadsRoot, url.Substring("/uploads/".Length).Replace('/', Path.DirectorySeparatorChar));

    private static UploadClientDocumentCommand MakeUpload(int clientId, ClientDocumentType type, byte[]? content = null)
    {
        var bytes = content ?? PngBytes;
        return new()
        {
            ClientId = clientId,
            DocumentType = type,
            FileName = "document.png",
            ContentType = "image/png",
            Length = bytes.Length,
            Content = new MemoryStream(bytes)
        };
    }

    // Resolves the StoredFile currently attached to a client's document slot.
    private static async Task<StoredFile?> DocumentFileAsync(int clientId, ClientDocumentType type)
    {
        var client = await FindAsync<Client>(clientId);
        var fileId = type switch
        {
            ClientDocumentType.CIN => client!.CINFileId,
            ClientDocumentType.DrivingLicence => client!.DrivingLicenceFileId,
            ClientDocumentType.Passeport => client!.PasseportFileId,
            _ => null
        };
        return fileId is int id ? await FindAsync<StoredFile>(id) : null;
    }

    private static async Task<int> CreateTestClientAsync()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        return await SendAsync(new CreateClientCommand
        {
            FirstName = "John",
            LastName = "Doe",
            BirthDate = new DateTime(1990, 5, 20)
        });
    }

    [Test]
    public async Task ShouldStoreFileAndCaptureRichMetadata()
    {
        var clientId = await CreateTestClientAsync();

        var url = await SendAsync(MakeUpload(clientId, ClientDocumentType.CIN));

        url.Should().StartWith("/uploads/");
        url.Should().EndWith(".png");

        var file = await DocumentFileAsync(clientId, ClientDocumentType.CIN);
        file.Should().NotBeNull();
        file!.Url.Should().Be(url);
        file.OriginalFileName.Should().Be("document.png");
        file.MimeType.Should().Be("image/png");
        file.Size.Should().Be(PngBytes.Length);
        file.DocumentType.Should().Be(DocumentType.CIN);
        // SHA-256 of the bytes, lowercase hex, always 64 chars.
        file.Sha256.Should().MatchRegex("^[0-9a-f]{64}$");

        File.Exists(StoredPath(url)).Should().BeTrue();
    }

    [Test]
    public async Task ShouldReplacePreviousDocument()
    {
        var clientId = await CreateTestClientAsync();

        // Distinct content so the replacement is a genuinely different file
        // (identical bytes would dedup and reuse the same physical file).
        var firstUrl = await SendAsync(MakeUpload(clientId, ClientDocumentType.Passeport, PngBytes));
        var secondUrl = await SendAsync(MakeUpload(clientId, ClientDocumentType.Passeport, OtherBytes));

        secondUrl.Should().NotBe(firstUrl);

        var file = await DocumentFileAsync(clientId, ClientDocumentType.Passeport);
        file!.Url.Should().Be(secondUrl);

        // The replaced file's record and bytes are both gone.
        File.Exists(StoredPath(firstUrl)).Should().BeFalse();
        (await CountAsync<StoredFile>(f => f.Url == firstUrl)).Should().Be(0);
        File.Exists(StoredPath(secondUrl)).Should().BeTrue();
    }

    [Test]
    public async Task ShouldDeleteStoredFilesWhenClientIsDeleted()
    {
        var clientId = await CreateTestClientAsync();

        var cinUrl = await SendAsync(MakeUpload(clientId, ClientDocumentType.CIN, PngBytes));
        var passeportUrl = await SendAsync(MakeUpload(clientId, ClientDocumentType.Passeport, OtherBytes));

        await SendAsync(new DeleteClientCommand(clientId));

        (await FindAsync<Client>(clientId)).Should().BeNull();
        File.Exists(StoredPath(cinUrl)).Should().BeFalse();
        File.Exists(StoredPath(passeportUrl)).Should().BeFalse();
        (await CountAsync<StoredFile>()).Should().Be(0);
    }

    [Test]
    public async Task ShouldDeduplicateIdenticalContentWithinAgency()
    {
        var clientId = await CreateTestClientAsync();

        // Same bytes uploaded to two slots: the second reuses the first file's
        // physical bytes rather than writing a second copy.
        var cinUrl = await SendAsync(MakeUpload(clientId, ClientDocumentType.CIN, PngBytes));
        var dlUrl = await SendAsync(MakeUpload(clientId, ClientDocumentType.DrivingLicence, PngBytes));

        dlUrl.Should().Be(cinUrl);

        var cinFile = await DocumentFileAsync(clientId, ClientDocumentType.CIN);
        var dlFile = await DocumentFileAsync(clientId, ClientDocumentType.DrivingLicence);

        // Two distinct metadata rows, one shared physical path/hash.
        cinFile!.Id.Should().NotBe(dlFile!.Id);
        dlFile.Sha256.Should().Be(cinFile.Sha256);
        dlFile.Path.Should().Be(cinFile.Path);
        (await CountAsync<StoredFile>()).Should().Be(2);
        File.Exists(StoredPath(cinUrl)).Should().BeTrue();
    }

    [Test]
    public async Task ShouldRejectDisallowedContentType()
    {
        await RunAsAgencyAdministratorAsync();

        var command = MakeUpload(1, ClientDocumentType.CIN) with
        {
            FileName = "malware.exe",
            ContentType = "application/octet-stream"
        };

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldRejectOversizedFile()
    {
        await RunAsAgencyAdministratorAsync();

        var command = MakeUpload(1, ClientDocumentType.CIN) with
        {
            Length = 6 * 1024 * 1024
        };

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }
}
