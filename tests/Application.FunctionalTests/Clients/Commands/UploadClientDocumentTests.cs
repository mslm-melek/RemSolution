using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Client.Commands.CreateClientCommand;
using RemSolution.Application.Features.Client.Commands.DeleteClientCommand;
using RemSolution.Application.Features.Client.Commands.RegenerateClientPortraitCommand;
using RemSolution.Application.Features.Client.Commands.UploadClientDocumentCommand;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using SkiaSharp;

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
    public async Task ShouldPreserveStoredFilesWhenClientIsArchived()
    {
        var clientId = await CreateTestClientAsync();

        var cinUrl = await SendAsync(MakeUpload(clientId, ClientDocumentType.CIN, PngBytes));
        var passeportUrl = await SendAsync(MakeUpload(clientId, ClientDocumentType.Passeport, OtherBytes));

        await SendAsync(new DeleteClientCommand(clientId));

        // Deleting a client archives it (history preserved); its identity
        // documents and their bytes are kept, not erased.
        (await CountAsync<Client>(c => c.Id == clientId)).Should().Be(0);
        (await FindIgnoringFiltersAsync<Client>(c => c.Id == clientId))!.IsDeleted.Should().BeTrue();
        File.Exists(StoredPath(cinUrl)).Should().BeTrue();
        File.Exists(StoredPath(passeportUrl)).Should().BeTrue();
        (await CountAsync<StoredFile>()).Should().Be(2);
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

    // --- The portrait cut out of the CIN --------------------------------------
    // PngBytes above is an 8-byte PNG signature and nothing more: it stores fine
    // but no decoder can read it, which is exactly the "CIN with no readable
    // photo on it" case (a PDF scan, the back of the card). The fixtures below
    // are real images, so they exercise the other branch.

    [Test]
    public async Task ShouldCutThePortraitOutOfAnUploadedCinImage()
    {
        var clientId = await CreateTestClientAsync();

        await SendAsync(MakeUpload(clientId, ClientDocumentType.CIN, IdCardImage()));

        var client = await FindAsync<Client>(clientId);
        client!.CINPortraitFileId.Should().NotBeNull();

        var portrait = await FindAsync<StoredFile>(client.CINPortraitFileId!.Value);
        portrait!.DocumentType.Should().Be(DocumentType.ClientPortrait);
        portrait.MimeType.Should().Be("image/jpeg");
        File.Exists(StoredPath(portrait.Url)).Should().BeTrue();

        // The document and the face derived from it: two files, not one.
        (await CountAsync<StoredFile>()).Should().Be(2);
    }

    [Test]
    public async Task ShouldNotCutAPortraitOutOfAnUnreadableCin()
    {
        var clientId = await CreateTestClientAsync();

        // A PDF scan or a photo of the back of the card: the document is stored,
        // there is simply no face on it. Not an error.
        await SendAsync(MakeUpload(clientId, ClientDocumentType.CIN, PngBytes));

        var client = await FindAsync<Client>(clientId);
        client!.CINFileId.Should().NotBeNull();
        client.CINPortraitFileId.Should().BeNull();
    }

    [Test]
    public async Task ShouldReplaceThePortraitWithTheCinItWasCutFrom()
    {
        var clientId = await CreateTestClientAsync();

        await SendAsync(MakeUpload(clientId, ClientDocumentType.CIN, IdCardImage()));
        var firstPortraitId = (await FindAsync<Client>(clientId))!.CINPortraitFileId;

        // A different card: the face on it is in the other corner, so the crop —
        // and therefore the stored bytes — genuinely differ.
        await SendAsync(MakeUpload(clientId, ClientDocumentType.CIN, IdCardImage(faceOnTheRight: true)));

        var portraitId = (await FindAsync<Client>(clientId))!.CINPortraitFileId;
        portraitId.Should().NotBeNull();
        portraitId.Should().NotBe(firstPortraitId);

        // The superseded portrait's record is gone, like the document's own.
        var staleId = firstPortraitId!.Value;
        (await CountAsync<StoredFile>(f => f.Id == staleId)).Should().Be(0);
    }

    [Test]
    public async Task ShouldClearThePortraitWhenTheNewCinHasNoFaceOnIt()
    {
        var clientId = await CreateTestClientAsync();

        await SendAsync(MakeUpload(clientId, ClientDocumentType.CIN, IdCardImage()));
        await SendAsync(MakeUpload(clientId, ClientDocumentType.CIN, PngBytes));

        // The old face belonged to the old card. Showing it beside the new one
        // would be worse than showing nothing.
        (await FindAsync<Client>(clientId))!.CINPortraitFileId.Should().BeNull();
    }

    [Test]
    public async Task ShouldRegenerateThePortraitFromTheCinAlreadyOnFile()
    {
        var clientId = await CreateTestClientAsync();
        await SendAsync(MakeUpload(clientId, ClientDocumentType.CIN, IdCardImage()));

        var supersededId = (await FindAsync<Client>(clientId))!.CINPortraitFileId!.Value;

        var result = await SendAsync(new RegenerateClientPortraitCommand(clientId));

        result.HasCinImage.Should().BeTrue();
        result.PortraitUrl.Should().NotBeNullOrEmpty();

        var refreshed = await FindAsync<Client>(clientId);
        refreshed!.CINPortraitFileId.Should().NotBeNull();
        refreshed.CINPortraitFileId.Should().NotBe(supersededId);
        (await FindAsync<StoredFile>(refreshed.CINPortraitFileId!.Value))!.Url
            .Should().Be(result.PortraitUrl);

        // The record it replaced is gone. Its BYTES are not: re-cropping an
        // unchanged image produces identical bytes, so the new record deduped onto
        // the same physical file — and the orphan check is what keeps a shared
        // file alive when only one of its references goes away.
        (await CountAsync<StoredFile>(f => f.Id == supersededId)).Should().Be(0);
        File.Exists(StoredPath(result.PortraitUrl!)).Should().BeTrue();
    }

    [Test]
    public async Task ShouldReportThatThereIsNothingToCropWhenTheClientHasNoCin()
    {
        var clientId = await CreateTestClientAsync();

        var result = await SendAsync(new RegenerateClientPortraitCommand(clientId));

        result.HasCinImage.Should().BeFalse();
        result.PortraitUrl.Should().BeNull();
    }

    // A picture of an identity card: card stock with a skin-coloured oval on it
    // where the holder's photo goes. Synthetic, because a test fixture must not
    // be a real person's identity papers.
    private static byte[] IdCardImage(bool faceOnTheRight = false)
    {
        using var bitmap = new SKBitmap(600, 380);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(221, 229, 220));

            var face = faceOnTheRight
                ? new SKRect(440, 110, 540, 250)
                : new SKRect(60, 110, 160, 250);

            using var paint = new SKPaint { Color = new SKColor(198, 134, 66), IsAntialias = true };
            canvas.DrawOval(face, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
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
