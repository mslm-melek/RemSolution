namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// Resizing seam for the car-image pipeline. Isolated behind an interface so the
/// background service stays testable and the imaging library (SkiaSharp) is a
/// single swappable implementation detail.
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Resizes <paramref name="source"/> to fit within a
    /// <paramref name="maxDimension"/>×<paramref name="maxDimension"/> box,
    /// preserving aspect ratio and never upscaling, and returns the result
    /// encoded as JPEG at <paramref name="quality"/> (1–100). Throws if the
    /// source bytes are not a decodable image.
    /// </summary>
    byte[] ResizeToJpeg(byte[] source, int maxDimension, int quality = 85);
}
