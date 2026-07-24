using RemSolution.Application.Common.Interfaces;
using SkiaSharp;

namespace RemSolution.Infrastructure.Imaging;

// SkiaSharp-backed resizer. Stateless and thread-safe → singleton.
public sealed class SkiaImageProcessor : IImageProcessor
{
    public byte[] ResizeToJpeg(byte[] source, int maxDimension, int quality = 85)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (maxDimension <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDimension));
        }

        // Decode throws (ArgumentNullException from the null codec) rather than
        // returning null on unrecognised bytes; normalise both to one contract.
        SKBitmap? decoded;
        try
        {
            decoded = SKBitmap.Decode(source);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("The source bytes are not a decodable image.", ex);
        }

        using var original = decoded
            ?? throw new InvalidOperationException("The source bytes are not a decodable image.");

        // Fit within the box; never upscale (scale capped at 1.0).
        var scale = Math.Min(1.0, (double)maxDimension / Math.Max(original.Width, original.Height));
        var width = Math.Max(1, (int)Math.Round(original.Width * scale));
        var height = Math.Max(1, (int)Math.Round(original.Height * scale));

        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

        using var resized = original.Resize(new SKImageInfo(width, height), sampling)
            ?? throw new InvalidOperationException("Image resize failed.");
        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);

        return data.ToArray();
    }
}
