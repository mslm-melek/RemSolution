using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RemSolution.Infrastructure.Storage;

namespace RemSolution.Infrastructure.IntegrationTests.Storage;

/// <summary>
/// The file store against a real disk. Everything here is a rule the interface
/// only describes in prose — that a saved URL reads back, that a URL from
/// somewhere else is refused, and above all that no relative path can be made to
/// escape the storage root, which is the one bug in a file store that matters.
/// </summary>
public class LocalFileStorageTests
{
    private string _root = null!;
    private LocalFileStorage _storage = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "RemSolutionStorageTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        // An absolute RootPath, so the host's content root is not consulted and
        // the test writes only where it says it does.
        _storage = new LocalFileStorage(
            Options.Create(new FileStorageOptions { RootPath = _root, PublicBasePath = "/uploads" }),
            new StubEnvironment(_root));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Test]
    public async Task SaveAsync_ShouldWriteTheBytesAndReturnAPublicUrl()
    {
        var url = await SaveAsync("agencies/1/clients/3/cin.jpg", "hello");

        url.Should().Be("/uploads/agencies/1/clients/3/cin.jpg");
        File.Exists(Path.Combine(_root, "agencies", "1", "clients", "3", "cin.jpg")).Should().BeTrue();
    }

    [Test]
    public async Task OpenReadAsync_ShouldReadBackWhatSaveWrote()
    {
        var url = await SaveAsync("agencies/1/a.txt", "round trip");

        await using var stream = await _storage.OpenReadAsync(url);
        using var reader = new StreamReader(stream);

        (await reader.ReadToEndAsync()).Should().Be("round trip");
    }

    [Test]
    public async Task SaveAsync_ShouldOverwriteTheSamePath()
    {
        await SaveAsync("agencies/1/a.txt", "first");
        var url = await SaveAsync("agencies/1/a.txt", "second");

        await using var stream = await _storage.OpenReadAsync(url);
        using var reader = new StreamReader(stream);

        (await reader.ReadToEndAsync()).Should().Be("second");
    }

    [Test]
    public async Task OpenReadAsync_ShouldRefuseAUrlItNeverIssued()
    {
        await SaveAsync("agencies/1/a.txt", "x");

        await FluentActions.Invoking(() => _storage.OpenReadAsync("https://example.com/a.txt"))
            .Should().ThrowAsync<FileNotFoundException>();
    }

    [Test]
    public async Task OpenReadAsync_ShouldThrowForMissingBytes()
    {
        await FluentActions.Invoking(() => _storage.OpenReadAsync("/uploads/agencies/1/gone.txt"))
            .Should().ThrowAsync<FileNotFoundException>();
    }

    [Test]
    public async Task DeleteAsync_ShouldRemoveTheFile()
    {
        var url = await SaveAsync("agencies/1/a.txt", "x");

        await _storage.DeleteAsync(url);

        File.Exists(Path.Combine(_root, "agencies", "1", "a.txt")).Should().BeFalse();
    }

    [Test]
    public async Task DeleteAsync_ShouldBeSilentAboutAFileThatIsNotThere()
    {
        await FluentActions.Invoking(() => _storage.DeleteAsync("/uploads/agencies/1/gone.txt"))
            .Should().NotThrowAsync();
    }

    /// <summary>
    /// An external URI is not ours to touch, so a delete must leave it alone
    /// rather than guess at a local path for it.
    /// </summary>
    [Test]
    public async Task DeleteAsync_ShouldIgnoreAForeignUrl()
    {
        await FluentActions.Invoking(() => _storage.DeleteAsync("https://cdn.example.com/a.jpg"))
            .Should().NotThrowAsync();
    }

    [TestCase("../escape.txt")]
    [TestCase("agencies/../../escape.txt")]
    [TestCase("agencies/./../../escape.txt")]
    [TestCase("..\\escape.txt")]
    [TestCase("")]
    [TestCase("/")]
    public async Task SaveAsync_ShouldRefuseAPathThatLeavesTheRoot(string relativePath)
    {
        await FluentActions.Invoking(() => SaveAsync(relativePath, "x"))
            .Should().ThrowAsync<ArgumentException>();

        // And nothing was written on the way to refusing.
        Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Test]
    public async Task SaveAsync_ShouldTreatBackslashesAsSeparators()
    {
        // A caller on Windows may hand over a Windows-shaped path; the URL is
        // forward-slashed either way.
        var url = await SaveAsync("agencies\\1\\a.txt", "x");

        url.Should().Be("/uploads/agencies/1/a.txt");
    }

    private Task<string> SaveAsync(string relativePath, string content)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return _storage.SaveAsync(stream, relativePath, "text/plain");
    }

    // The store only reads ContentRootPath, and only for a relative RootPath.
    private sealed class StubEnvironment : IHostEnvironment
    {
        public StubEnvironment(string contentRoot) => ContentRootPath = contentRoot;

        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; }
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
