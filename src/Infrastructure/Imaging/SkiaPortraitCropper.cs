using RemSolution.Application.Common.Interfaces;
using SkiaSharp;

namespace RemSolution.Infrastructure.Imaging;

/// <summary>
/// Finds the holder's photo on a picture of an identity document by looking for
/// their face, and cuts a square head-and-shoulders portrait out of it.
/// <para>
/// No face-recognition model is involved, and none is wanted here: this runs on
/// a scan of a card where the holder's photo is the one region of human skin, so
/// the cheap classical answer is enough. The scan marks skin-coloured pixels,
/// groups them into regions, and keeps the largest region that is shaped like a
/// face — solid, roughly as tall as it is wide, and a small part of the picture
/// rather than most of it. That last test is what rejects a beige or pink card
/// background, which otherwise reads as one enormous "face".
/// </para>
/// <para>
/// Skin is detected in YCbCr rather than RGB. Chrominance barely moves across
/// skin tones while luminance moves a great deal, so a chrominance window plus
/// two tone-independent sanity checks (skin is warmer than it is blue, and never
/// neutral grey) recognises dark and light skin alike. The absolute red
/// thresholds of the textbook RGB rules do not, which is why they are not used.
/// </para>
/// <para>
/// When no region passes, the crop falls back to where the photo sits on
/// essentially every ID card — see <see cref="ConventionalPortraitRect"/>. A
/// crop of the right corner of the card is a far better answer than no picture
/// at all, and the caller can always replace it.
/// </para>
/// Stateless and thread-safe → singleton.
/// </summary>
public sealed class SkiaPortraitCropper : IPortraitCropper
{
    // The scan runs on a downscaled copy: a face is a large, blobby thing, so
    // 320px of detail is plenty to find it, and it keeps the region grouping
    // below to ~100k pixels regardless of what a phone camera produced.
    private const int ScanMaxPx = 320;

    // Below this there is nothing to crop out — the picture is already smaller
    // than the portrait we would produce.
    private const int MinimumSourcePx = 64;

    // A face region must be at least this share of the picture (anything smaller
    // is a speck of noise) and its bounding box at most this share (anything
    // bigger is the card itself, not a face on it).
    private const double MinRegionArea = 0.003;
    private const double MaxRegionBoxArea = 0.35;

    // A face is a solid patch, so most of its bounding box is skin. Lettering,
    // guilloche patterns and JPEG fringing are scattered and fail this.
    private const double MinRegionFill = 0.45;

    public byte[]? CropIdentityPortraitToJpeg(byte[] source, int size = 256, int quality = 85)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

        // A PDF scan or a corrupt upload lands here, and both are ordinary: the
        // document is still stored, it just has no face on it we can read.
        using var decoded = TryDecodeOriented(source);
        if (decoded is null || decoded.Width < MinimumSourcePx || decoded.Height < MinimumSourcePx)
        {
            return null;
        }

        var crop = DetectPortraitRect(decoded);

        // ExtractSubset shares the source's pixels, so `decoded` must outlive it.
        using var subset = new SKBitmap();
        if (!decoded.ExtractSubset(subset, crop))
        {
            return null;
        }

        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        using var square = subset.Resize(new SKImageInfo(size, size), sampling);
        if (square is null)
        {
            return null;
        }

        using var image = SKImage.FromBitmap(square);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);

        return data.ToArray();
    }

    /// <summary>
    /// The square to cut out of <paramref name="bitmap"/>: the detected head if
    /// one was found, otherwise the conventional photo corner of an ID card.
    /// Always a non-empty rectangle inside the bitmap. Public so the geometry can
    /// be asserted directly in tests.
    /// </summary>
    public static SKRectI DetectPortraitRect(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        // Scan a downscaled copy, then map the answer back up. Scale is the
        // scan-to-source ratio, so dividing by it returns source coordinates.
        var scale = Math.Min(1.0, (double)ScanMaxPx / Math.Max(bitmap.Width, bitmap.Height));
        var scanWidth = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        var scanHeight = Math.Max(1, (int)Math.Round(bitmap.Height * scale));

        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
        using var scan = scale < 1.0
            ? bitmap.Resize(new SKImageInfo(scanWidth, scanHeight), sampling)
            : null;

        var working = scan ?? bitmap;
        var face = FindFaceRegion(working);

        if (face is null)
        {
            return ConventionalPortraitRect(bitmap);
        }

        var box = face.Value;

        // The detected region is the bare skin of the face — brow to chin, cheek
        // to cheek. A portrait wants the whole head, so the square is grown well
        // past it and pushed upwards: hair and forehead take up more room above
        // the skin than the chin and neck do below it.
        var side = Math.Max(box.Width, box.Height) * 1.9;
        var centreX = box.MidX;
        var centreY = box.MidY - box.Height * 0.22;

        // Back to source coordinates before clamping, so the clamp is against the
        // real bitmap rather than the scan's rounded-off bounds.
        var invScale = working == bitmap ? 1.0 : 1.0 / scale;

        return SquareWithin(
            bitmap.Width, bitmap.Height,
            centreX * invScale, centreY * invScale, side * invScale);
    }

    /// <summary>
    /// Where the holder's photo sits when we could not find a face: on a card
    /// held landscape, the left-hand third — the layout of every CIN, passport
    /// page and driving licence this app sees. A picture that is not
    /// card-shaped is far more likely to be a photo of the person themselves, so
    /// that one is cropped head-on instead.
    /// </summary>
    public static SKRectI ConventionalPortraitRect(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var width = (double)bitmap.Width;
        var height = (double)bitmap.Height;
        var landscape = width >= height * 1.25;

        return landscape
            ? SquareWithin(bitmap.Width, bitmap.Height, width * 0.20, height * 0.46, height * 0.62)
            : SquareWithin(bitmap.Width, bitmap.Height, width * 0.50, height * 0.40,
                           Math.Min(width, height) * 0.62);
    }

    // --- Skin regions ---------------------------------------------------------

    // The largest skin-coloured region shaped like a face, in the given bitmap's
    // own coordinates; null when nothing qualifies.
    private static SKRect? FindFaceRegion(SKBitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var total = width * height;

        var pixels = bitmap.Pixels;
        var isSkin = new bool[total];
        var skinCount = 0;

        for (var i = 0; i < total; i++)
        {
            if (IsSkin(pixels[i]))
            {
                isSkin[i] = true;
                skinCount++;
            }
        }

        // Nothing skin-coloured at all: a greyscale scan, or the back of a card.
        if (skinCount < total * MinRegionArea)
        {
            return null;
        }

        var visited = new bool[total];
        var queue = new int[total];
        SKRect? best = null;
        var bestArea = 0;

        for (var start = 0; start < total; start++)
        {
            if (!isSkin[start] || visited[start])
            {
                continue;
            }

            // Flood the region breadth-first over 4-connected neighbours, keeping
            // its extent and pixel count. Iterative, not recursive: a region can
            // cover the whole picture.
            var head = 0;
            var tail = 0;
            queue[tail++] = start;
            visited[start] = true;

            int minX = width, minY = height, maxX = -1, maxY = -1, area = 0;

            while (head < tail)
            {
                var index = queue[head++];
                var x = index % width;
                var y = index / width;

                area++;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;

                // 4-connected: the diagonals would bridge a face to the lettering
                // beside it through a single corner-touching pixel.
                Span<int> neighbours =
                [
                    x > 0 ? index - 1 : -1,
                    x < width - 1 ? index + 1 : -1,
                    y > 0 ? index - width : -1,
                    y < height - 1 ? index + width : -1
                ];

                foreach (var neighbour in neighbours)
                {
                    if (neighbour >= 0 && isSkin[neighbour] && !visited[neighbour])
                    {
                        visited[neighbour] = true;
                        queue[tail++] = neighbour;
                    }
                }
            }

            if (area <= bestArea || !LooksLikeAFace(area, minX, minY, maxX, maxY, total))
            {
                continue;
            }

            bestArea = area;
            best = new SKRect(minX, minY, maxX + 1, maxY + 1);
        }

        return best;
    }

    private static bool LooksLikeAFace(int area, int minX, int minY, int maxX, int maxY, int total)
    {
        var boxWidth = maxX - minX + 1;
        var boxHeight = maxY - minY + 1;
        var boxArea = (double)boxWidth * boxHeight;

        if (area < total * MinRegionArea || boxArea > total * MaxRegionBoxArea)
        {
            return false;
        }

        if (area / boxArea < MinRegionFill)
        {
            return false;
        }

        // Brow-to-chin skin is taller than it is wide, or nearly square once the
        // cheeks and neck join it. A long thin streak is a border or a shadow.
        var aspect = (double)boxWidth / boxHeight;

        return aspect is >= 0.4 and <= 1.8;
    }

    // Skin in YCbCr: the classic chrominance window, plus two checks that hold
    // for every skin tone — skin reflects more red than blue, and is never
    // neutral grey. Deliberately no absolute red/green thresholds: those are what
    // make the textbook RGB rules fail on dark skin.
    private static bool IsSkin(SKColor colour)
    {
        int r = colour.Red, g = colour.Green, b = colour.Blue;

        if (colour.Alpha < 128 || r <= b)
        {
            return false;
        }

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));

        if (max - min <= 10)
        {
            return false;
        }

        var y = 0.299 * r + 0.587 * g + 0.114 * b;
        var cb = 128.0 - 0.168736 * r - 0.331264 * g + 0.5 * b;
        var cr = 128.0 + 0.5 * r - 0.418688 * g - 0.081312 * b;

        return y is >= 40 and <= 250
            && cb is >= 77 and <= 127
            && cr is >= 133 and <= 175;
    }

    // --- Geometry -------------------------------------------------------------

    // The largest square of the requested side centred as near (centreX, centreY)
    // as fits inside the bitmap: slid back inside where it overhangs an edge, and
    // only shrunk when it is wider than the bitmap itself.
    private static SKRectI SquareWithin(int width, int height, double centreX, double centreY, double side)
    {
        side = Math.Clamp(side, 1, Math.Min(width, height));

        var left = Math.Clamp(centreX - side / 2, 0, width - side);
        var top = Math.Clamp(centreY - side / 2, 0, height - side);

        var length = Math.Max(1, (int)Math.Round(side));
        var x = Math.Clamp((int)Math.Round(left), 0, width - length);
        var y = Math.Clamp((int)Math.Round(top), 0, height - length);

        return new SKRectI(x, y, x + length, y + length);
    }

    // --- Decoding -------------------------------------------------------------

    // Decodes and turns the picture the right way up. Cards are usually
    // photographed rather than scanned, and a phone records the rotation in EXIF
    // instead of applying it — a portrait held sideways would have its face
    // hunted for in the wrong third of the image.
    private static SKBitmap? TryDecodeOriented(byte[] source)
    {
        SKBitmap? bitmap;
        try
        {
            bitmap = SKBitmap.Decode(source);
        }
        catch
        {
            // Decode throws (rather than returning null) on some unrecognised
            // bytes; both mean the same thing here.
            return null;
        }

        if (bitmap is null)
        {
            return null;
        }

        var degrees = RotationFor(source);
        if (degrees == 0)
        {
            return bitmap;
        }

        using (bitmap)
        {
            return Rotate(bitmap, degrees);
        }
    }

    private static int RotationFor(byte[] source)
    {
        try
        {
            using var data = SKData.CreateCopy(source);
            using var codec = SKCodec.Create(data);

            return codec?.EncodedOrigin switch
            {
                SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom => 90,
                SKEncodedOrigin.BottomRight or SKEncodedOrigin.BottomLeft => 180,
                SKEncodedOrigin.LeftBottom or SKEncodedOrigin.LeftTop => 270,
                _ => 0
            };
        }
        catch
        {
            // No EXIF, or a format with no codec of its own: assume upright.
            return 0;
        }
    }

    private static SKBitmap Rotate(SKBitmap source, int degrees)
    {
        var quarterTurn = degrees is 90 or 270;
        var width = quarterTurn ? source.Height : source.Width;
        var height = quarterTurn ? source.Width : source.Height;

        var rotated = new SKBitmap(width, height);
        using var canvas = new SKCanvas(rotated);

        canvas.Translate(width / 2f, height / 2f);
        canvas.RotateDegrees(degrees);
        canvas.Translate(-source.Width / 2f, -source.Height / 2f);
        canvas.DrawBitmap(source, 0, 0);

        return rotated;
    }
}
