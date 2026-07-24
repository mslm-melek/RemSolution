using System.Text;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Features.Car.Commands.CreateCarCommand;
using RemSolution.Application.Common.Exceptions;
using RemSolution.Application.Features.Car.Commands.DeleteCarImageCommand;
using RemSolution.Application.Features.Car.Commands.ReorderCarImagesCommand;
using RemSolution.Application.Features.Car.Commands.SetPrimaryCarImageCommand;
using RemSolution.Application.Features.Car.Commands.UploadCarImageCommand;
using RemSolution.Application.Features.Car.DTOs;
using RemSolution.Application.Features.Car.Queries.GetCarImagesQuery;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace RemSolution.Application.FunctionalTests.Cars.Commands;

using static Testing;

public class CarGalleryTests : BaseTestFixture
{
    [Test]
    public async Task UploadShouldStoreOriginalMakeFirstImagePrimaryAndEnqueueProcessing()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var carId = await CreateCarAsync("UP-001");

        var first = await UploadAsync(carId, "first.jpg", "first-bytes");

        first.IsPrimary.Should().BeTrue("the first image of a car is primary");
        first.SortOrder.Should().Be(0);
        first.OriginalUrl.Should().NotBeNullOrEmpty();
        first.ProcessingStatus.Should().Be(ImageProcessingStatus.Pending);
        // Derivatives are produced out of band, so they are absent synchronously.
        first.ThumbnailUrl.Should().BeNull();
        first.MediumUrl.Should().BeNull();
        RecordingImageProcessingQueue.EnqueuedFor(first.Id)
            .Should().BeTrue("the original is committed then derivative generation is enqueued");

        var second = await UploadAsync(carId, "second.jpg", "second-bytes");

        second.IsPrimary.Should().BeFalse("only the first image is primary by default");
        second.SortOrder.Should().Be(1, "new images append to the end of the gallery");
        RecordingImageProcessingQueue.EnqueuedFor(second.Id).Should().BeTrue();

        (await CountAsync<CarImage>()).Should().Be(2);
    }

    [Test]
    public async Task SetPrimaryShouldMoveTheFlagToTheChosenImage()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var carId = await CreateCarAsync("PRI-001");

        var a = await UploadAsync(carId, "a.jpg", "a-bytes");
        var b = await UploadAsync(carId, "b.jpg", "b-bytes");

        await SendAsync(new SetPrimaryCarImageCommand(carId, b.Id));

        var images = await GetImagesAsync(carId);
        images.Single(i => i.Id == b.Id).IsPrimary.Should().BeTrue();
        images.Single(i => i.Id == a.Id).IsPrimary.Should().BeFalse();
    }

    [Test]
    public async Task SetPrimaryShouldThrowForAnImageThatIsNotOnTheCar()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var carId = await CreateCarAsync("PRI-404");
        await UploadAsync(carId, "a.jpg", "a-bytes");

        var act = async () => await SendAsync(new SetPrimaryCarImageCommand(carId, 999999));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task DeletingPrimaryImageShouldPromoteNextAndRemoveItsBytes()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var carId = await CreateCarAsync("DIMG-001");

        var primary = await UploadAsync(carId, "primary.jpg", "primary-bytes");
        var other = await UploadAsync(carId, "other.jpg", "other-bytes");

        await SendAsync(new DeleteCarImageCommand(carId, primary.Id));

        // The row and its stored original are gone.
        (await CountAsync<CarImage>(i => i.Id == primary.Id)).Should().Be(0);
        (await CountAsync<CarImage>()).Should().Be(1);
        await AssertBytesExist(new[] { primary.OriginalUrl! }, exist: false);

        // The survivor is promoted to primary and keeps its bytes.
        var images = await GetImagesAsync(carId);
        images.Single().Id.Should().Be(other.Id);
        images.Single().IsPrimary.Should().BeTrue("deleting the primary promotes the next image");
        await AssertBytesExist(new[] { other.OriginalUrl! }, exist: true);
    }

    [Test]
    public async Task DeletingNonPrimaryImageShouldLeaveThePrimaryUnchanged()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var carId = await CreateCarAsync("DIMG-002");

        var primary = await UploadAsync(carId, "primary.jpg", "keep-bytes");
        var extra = await UploadAsync(carId, "extra.jpg", "drop-bytes");

        await SendAsync(new DeleteCarImageCommand(carId, extra.Id));

        var images = await GetImagesAsync(carId);
        images.Single().Id.Should().Be(primary.Id);
        images.Single().IsPrimary.Should().BeTrue();
        await AssertBytesExist(new[] { extra.OriginalUrl! }, exist: false);
        await AssertBytesExist(new[] { primary.OriginalUrl! }, exist: true);
    }

    [Test]
    public async Task ReorderShouldAssignSortOrderFromTheGivenSequence()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var carId = await CreateCarAsync("ORD-001");

        var a = await UploadAsync(carId, "a.jpg", "a-bytes"); // SortOrder 0
        var b = await UploadAsync(carId, "b.jpg", "b-bytes"); // SortOrder 1
        var c = await UploadAsync(carId, "c.jpg", "c-bytes"); // SortOrder 2

        await SendAsync(new ReorderCarImagesCommand(carId, new[] { c.Id, a.Id, b.Id }));

        var images = await GetImagesAsync(carId); // query orders by SortOrder
        images.Select(i => i.Id).Should().ContainInOrder(c.Id, a.Id, b.Id);
        images.Single(i => i.Id == c.Id).SortOrder.Should().Be(0);
        images.Single(i => i.Id == a.Id).SortOrder.Should().Be(1);
        images.Single(i => i.Id == b.Id).SortOrder.Should().Be(2);
    }

    [Test]
    public async Task ReorderShouldRejectAListThatIsNotAnExactPermutation()
    {
        await RunAsAgencyAdministratorAsync();
        await AddTestAgencyAsync();
        var carId = await CreateCarAsync("ORD-400");

        var a = await UploadAsync(carId, "a.jpg", "a-bytes");
        var b = await UploadAsync(carId, "b.jpg", "b-bytes");

        // Missing b, plus an id that is not on the car.
        var act = async () => await SendAsync(new ReorderCarImagesCommand(carId, new[] { a.Id, 999999 }));

        await act.Should().ThrowAsync<ValidationException>();

        // Order is unchanged.
        var images = await GetImagesAsync(carId);
        images.Select(i => i.Id).Should().ContainInOrder(a.Id, b.Id);
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

    private static async Task<CarImageDto> UploadAsync(int carId, string fileName, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        await using var stream = new MemoryStream(bytes);

        return await SendAsync(new UploadCarImageCommand
        {
            CarId = carId,
            FileName = fileName,
            ContentType = "image/jpeg",
            Length = bytes.Length,
            Content = stream
        });
    }

    private static Task<IList<CarImageDto>> GetImagesAsync(int carId) =>
        SendAsync(new GetCarImagesQuery(carId));

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
