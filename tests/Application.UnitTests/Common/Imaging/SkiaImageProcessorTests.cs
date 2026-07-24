using FluentAssertions;
using NUnit.Framework;
using RemSolution.Infrastructure.Imaging;
using SkiaSharp;

namespace RemSolution.Application.UnitTests.Common.Imaging;

public class SkiaImageProcessorTests
{
    private readonly SkiaImageProcessor _sut = new();

    private static byte[] MakePng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.CornflowerBlue);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [Test]
    public void ResizeToJpeg_FitsWithinBox_PreservingAspectRatio()
    {
        var source = MakePng(400, 200);

        var output = _sut.ResizeToJpeg(source, 100);

        using var result = SKBitmap.Decode(output);
        result.Width.Should().Be(100);
        result.Height.Should().Be(50);
    }

    [Test]
    public void ResizeToJpeg_DoesNotUpscaleSmallImages()
    {
        var source = MakePng(50, 40);

        var output = _sut.ResizeToJpeg(source, 100);

        using var result = SKBitmap.Decode(output);
        result.Width.Should().Be(50);
        result.Height.Should().Be(40);
    }

    [Test]
    public void ResizeToJpeg_Throws_OnUndecodableBytes()
    {
        var act = () => _sut.ResizeToJpeg(new byte[] { 1, 2, 3 }, 100);

        act.Should().Throw<InvalidOperationException>();
    }
}
