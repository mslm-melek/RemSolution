using System.Text;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Car.Commands.CreateCarCommand;
using RemSolution.Application.Features.Car.Commands.DeleteCarCommand;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace RemSolution.Application.FunctionalTests.Cars.Commands;

using static Testing;

public class DeleteCarTests : BaseTestFixture
{
    [Test]
    public async Task ShouldArchiveCarAndHideItFromReads()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var carId = await CreateCarAsync("DEL-999");

        await SendAsync(new DeleteCarCommand(carId));

        // Hidden from normal (filtered) reads...
        (await CountAsync<Car>(c => c.Id == carId)).Should().Be(0);

        // ...but the row survives, flagged, with who/when stamped.
        var archived = await FindIgnoringFiltersAsync<Car>(c => c.Id == carId);
        archived.Should().NotBeNull();
        archived!.IsDeleted.Should().BeTrue();
        archived.DeletedAt.Should().NotBeNull();
        archived.DeletedBy.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task ArchivingACarPreservesItsGalleryImagesAndBytes()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();

        var carId = await CreateCarAsync("GAL-001");
        var urls = await AddGalleryImageAsync(carId, "original-1", "thumb-1", "medium-1");

        await SendAsync(new DeleteCarCommand(carId));

        // History is preserved: the image row and its stored files stay, bytes
        // still on disk (archiving is not a delete).
        (await CountAsync<Car>(c => c.Id == carId)).Should().Be(0);
        (await CountAsync<CarImage>()).Should().Be(1);
        (await CountAsync<StoredFile>()).Should().Be(3);
        await AssertBytesExist(urls.All, exist: true);
    }

    private static async Task<int> CreateCarAsync(string matricule)
    {
        var brand = new Brand { Name = $"Brand-{matricule}" };
        await AddAsync(brand);

        var model = new ModelCar { Name = $"Model-{matricule}", BrandId = brand.Id };
        await AddAsync(model);

        return await SendAsync(new CreateCarCommand
        {
            Matricule = matricule,
            ModelId = model.Id,
            Color = "Gray",
            FirstCirculationDate = DateTime.UtcNow
        });
    }

    private sealed record ImageUrls(string Original, string Thumbnail, string Medium)
    {
        public string[] All => new[] { Original, Thumbnail, Medium };
    }

    private static Task<ImageUrls> AddGalleryImageAsync(
        int carId, string original, string thumbnail, string medium) =>
        UsingScopeAsync(async sp =>
        {
            var storedFiles = sp.GetRequiredService<IStoredFileService>();
            var context = sp.GetRequiredService<RemSolution.Infrastructure.Data.ApplicationDbContext>();

            var basePath = $"agencies/{GetAgencyId()}/cars/{carId}/images/{Guid.NewGuid():N}";

            var originalFile = await CreateStoredFileAsync(storedFiles, original, $"{basePath}/original.jpg");
            var thumbnailFile = await CreateStoredFileAsync(storedFiles, thumbnail, $"{basePath}/thumb.jpg");
            var mediumFile = await CreateStoredFileAsync(storedFiles, medium, $"{basePath}/medium.jpg");

            context.Add(new CarImage
            {
                CarId = carId,
                OriginalFile = originalFile,
                ThumbnailFile = thumbnailFile,
                MediumFile = mediumFile,
                SortOrder = 0,
                IsPrimary = true,
                ProcessingStatus = ImageProcessingStatus.Completed
            });

            await context.SaveChangesAsync();

            return new ImageUrls(originalFile.Url, thumbnailFile.Url, mediumFile.Url);
        });

    private static async Task<StoredFile> CreateStoredFileAsync(
        IStoredFileService storedFiles, string content, string relativePath)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return await storedFiles.CreateAsync(
            stream, Path.GetFileName(relativePath), "image/jpeg",
            DocumentType.CarPhoto, relativePath, CancellationToken.None);
    }

    private static Task AssertBytesExist(IEnumerable<string> urls, bool exist) =>
        UsingScopeAsync<bool>(async sp =>
        {
            var storage = sp.GetRequiredService<IFileStorage>();

            foreach (var url in urls)
            {
                var open = async () =>
                {
                    await using var stream = await storage.OpenReadAsync(url);
                };

                if (exist)
                    await open.Should().NotThrowAsync($"bytes for '{url}' should still exist");
                else
                    await open.Should().ThrowAsync<FileNotFoundException>($"bytes for '{url}' should be deleted");
            }

            return true;
        });
}
