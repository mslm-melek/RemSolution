using RemSolution.Infrastructure.Imaging;
using SkiaSharp;

namespace RemSolution.Infrastructure.IntegrationTests.Imaging;

/// <summary>
/// The resizer against the real SkiaSharp native library — which is the point of
/// testing it here rather than in a unit test: the failure this catches is a
/// missing or mismatched native asset on the deployment platform, and no mock
/// would ever notice.
/// </summary>
public class SkiaImageProcessorTests
{
    private readonly SkiaImageProcessor _processor = new();

    [Test]
    public void ResizeToJpeg_ShouldFitALandscapeImageInTheBox()
    {
        var result = _processor.ResizeToJpeg(APng(1600, 900), maxDimension: 800);

        var size = SizeOf(result);
        size.Width.Should().Be(800);
        // 900 × (800/1600) = 450, aspect ratio preserved.
        size.Height.Should().Be(450);
    }

    [Test]
    public void ResizeToJpeg_ShouldFitAPortraitImageInTheBox()
    {
        var size = SizeOf(_processor.ResizeToJpeg(APng(900, 1600), maxDimension: 800));

        size.Height.Should().Be(800);
        size.Width.Should().Be(450);
    }

    /// <summary>
    /// Never upscale: a phone photo smaller than the thumbnail box must come back
    /// as it went in, not blown up and blurred.
    /// </summary>
    [Test]
    public void ResizeToJpeg_ShouldNotUpscaleASmallImage()
    {
        var size = SizeOf(_processor.ResizeToJpeg(APng(120, 80), maxDimension: 800));

        size.Width.Should().Be(120);
        size.Height.Should().Be(80);
    }

    [Test]
    public void ResizeToJpeg_ShouldAlwaysProduceJpeg()
    {
        // The source is a PNG; the derivative contract is JPEG whatever came in.
        var result = _processor.ResizeToJpeg(APng(400, 400), maxDimension: 200);

        using var codec = SKCodec.Create(new MemoryStream(result));
        codec!.EncodedFormat.Should().Be(SKEncodedImageFormat.Jpeg);
    }

    [Test]
    public void ResizeToJpeg_ShouldProduceTheTwoDerivativeSizesThePipelineAsks_For()
    {
        // The sizes CarImageProcessingJob actually requests.
        var original = APng(2400, 1600);

        SizeOf(_processor.ResizeToJpeg(original, 200)).Width.Should().Be(200);
        SizeOf(_processor.ResizeToJpeg(original, 800)).Width.Should().Be(800);
    }

    [Test]
    public void ResizeToJpeg_ALowerQuality_ShouldProduceFewerBytes()
    {
        var original = APng(600, 600);

        _processor.ResizeToJpeg(original, 400, quality: 40).Length
            .Should().BeLessThan(_processor.ResizeToJpeg(original, 400, quality: 95).Length);
    }

    [Test]
    public void ResizeToJpeg_ShouldRejectBytesThatAreNotAnImage()
    {
        var notAnImage = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        FluentActions.Invoking(() => _processor.ResizeToJpeg(notAnImage, 200))
            .Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ResizeToJpeg_ShouldRejectEmptyBytes()
    {
        FluentActions.Invoking(() => _processor.ResizeToJpeg(Array.Empty<byte>(), 200))
            .Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ResizeToJpeg_ShouldRejectANonPositiveBox()
    {
        FluentActions.Invoking(() => _processor.ResizeToJpeg(APng(100, 100), 0))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    // A real encoded PNG, not a fixture file: the test stays self-contained and
    // the dimensions are part of the assertion rather than of a binary blob.
    private static byte[] APng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.CornflowerBlue);

        // Something non-uniform, so a resize has actual detail to interpolate and
        // the quality assertion above is not comparing two flat images.
        using var paint = new SKPaint { Color = SKColors.Orange };
        canvas.DrawCircle(width / 2f, height / 2f, Math.Min(width, height) / 3f, paint);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }

    private static SKImageInfo SizeOf(byte[] jpeg)
    {
        using var codec = SKCodec.Create(new MemoryStream(jpeg));

        codec.Should().NotBeNull("the processor must return decodable bytes");

        return codec!.Info;
    }
}
