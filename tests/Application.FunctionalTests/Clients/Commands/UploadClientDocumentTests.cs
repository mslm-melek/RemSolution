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

    // Maps a returned /uploads/... URL to its on-disk location under the
    // test-isolated storage root configured in CustomWebApplicationFactory.
    private static string StoredPath(string url) =>
        Path.Combine(UploadsRoot, url.Substring("/uploads/".Length).Replace('/', Path.DirectorySeparatorChar));

    private static UploadClientDocumentCommand MakeUpload(int clientId, ClientDocumentType type) => new()
    {
        ClientId = clientId,
        DocumentType = type,
        FileName = "document.png",
        ContentType = "image/png",
        Length = PngBytes.Length,
        Content = new MemoryStream(PngBytes)
    };

    private static async Task<int> CreateTestClientAsync()
    {
        await AddTestAgencyAsync();

        return await SendAsync(new CreateClientCommand
        {
            FirstName = "John",
            LastName = "Doe",
            BirthDate = new DateTime(1990, 5, 20)
        });
    }

    [Test]
    public async Task ShouldStoreFileAndSetUrl()
    {
        var clientId = await CreateTestClientAsync();

        var url = await SendAsync(MakeUpload(clientId, ClientDocumentType.CIN));

        url.Should().StartWith("/uploads/");
        url.Should().EndWith(".png");

        var client = await FindAsync<Client>(clientId);
        client!.CINImageUrl.Should().Be(url);

        File.Exists(StoredPath(url)).Should().BeTrue();
    }

    [Test]
    public async Task ShouldReplacePreviousDocument()
    {
        var clientId = await CreateTestClientAsync();

        var firstUrl = await SendAsync(MakeUpload(clientId, ClientDocumentType.Passeport));
        var secondUrl = await SendAsync(MakeUpload(clientId, ClientDocumentType.Passeport));

        secondUrl.Should().NotBe(firstUrl);

        var client = await FindAsync<Client>(clientId);
        client!.PasserportImageUrl.Should().Be(secondUrl);

        File.Exists(StoredPath(firstUrl)).Should().BeFalse();
        File.Exists(StoredPath(secondUrl)).Should().BeTrue();
    }

    [Test]
    public async Task ShouldDeleteStoredFilesWhenClientIsDeleted()
    {
        var clientId = await CreateTestClientAsync();

        var cinUrl = await SendAsync(MakeUpload(clientId, ClientDocumentType.CIN));
        var passeportUrl = await SendAsync(MakeUpload(clientId, ClientDocumentType.Passeport));

        await SendAsync(new DeleteClientCommand(clientId));

        (await FindAsync<Client>(clientId)).Should().BeNull();
        File.Exists(StoredPath(cinUrl)).Should().BeFalse();
        File.Exists(StoredPath(passeportUrl)).Should().BeFalse();
    }

    [Test]
    public async Task ShouldRejectDisallowedContentType()
    {
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
        var command = MakeUpload(1, ClientDocumentType.CIN) with
        {
            Length = 6 * 1024 * 1024
        };

        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }
}
