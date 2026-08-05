namespace RemSolution.Application.Common.Interfaces;

/// <summary>
/// Cuts the holder's photo out of a picture of an identity document, so the app
/// can show a client's face where it only has a scan of their card.
/// <para>
/// A second seam beside <see cref="IImageProcessor"/> rather than another method
/// on it: resizing is arithmetic that always succeeds on a decodable image,
/// while locating a face is a guess that can come up empty — the two have
/// different contracts, and only this one is worth swapping for a real detector
/// later.
/// </para>
/// </summary>
public interface IPortraitCropper
{
    /// <summary>
    /// Locates the head on <paramref name="source"/> and returns it as a square
    /// JPEG <paramref name="size"/> pixels on a side, or <c>null</c> when the
    /// bytes hold no usable image (a PDF scan, a corrupt upload) or no plausible
    /// portrait could be found on it.
    /// <para>
    /// Null rather than an exception because a missing portrait is an ordinary
    /// outcome — plenty of legitimate CIN uploads are PDFs or pictures of the
    /// back of the card, and the caller's answer to all of them is the same:
    /// store the document, skip the face.
    /// </para>
    /// </summary>
    byte[]? CropIdentityPortraitToJpeg(byte[] source, int size = 256, int quality = 85);
}
