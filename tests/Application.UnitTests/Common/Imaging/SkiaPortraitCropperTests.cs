using FluentAssertions;
using NUnit.Framework;
using RemSolution.Infrastructure.Imaging;
using SkiaSharp;

namespace RemSolution.Application.UnitTests.Common.Imaging;

// The cropper's job is to find a face on a picture of an ID card, so the fixtures
// here are pictures of ID cards: a card-coloured rectangle with a skin-coloured
// oval on it. Synthetic rather than a checked-in scan of a real document — a test
// fixture must not be somebody's identity papers.
public class SkiaPortraitCropperTests
{
    private readonly SkiaPortraitCropper _sut = new();

    // Card stock: light and desaturated. Deliberately NOT skin — its channels are
    // within 9 of each other, so it fails the "never neutral grey" test.
    private static readonly SKColor CardStock = new(221, 229, 220);

    // Two skin tones, light-brown and dark-brown, because the detector claims to
    // recognise both and that claim is worth a test of its own.
    private static readonly SKColor MidBrownSkin = new(198, 134, 66);
    private static readonly SKColor DarkBrownSkin = new(74, 44, 26);

    private const int CardWidth = 600;
    private const int CardHeight = 380;

    // Well past the middle of the card — and well past where the fallback would
    // look, which is the whole point of the tests that use it.
    private const int RightOfCentre = (int)(CardWidth * 0.6);

    [Test]
    public void CropIdentityPortraitToJpeg_ReturnsASquareJpegOfTheRequestedSize()
    {
        var card = MakeCard(FaceOnTheLeft, MidBrownSkin);

        var output = _sut.CropIdentityPortraitToJpeg(card, size: 128);

        output.Should().NotBeNull();
        using var result = SKBitmap.Decode(output);
        result.Width.Should().Be(128);
        result.Height.Should().Be(128);
    }

    [Test]
    public void CropIdentityPortraitToJpeg_CentresTheCropOnTheFace()
    {
        var card = MakeCard(FaceOnTheLeft, MidBrownSkin);

        var output = _sut.CropIdentityPortraitToJpeg(card, size: 64);

        using var result = SKBitmap.Decode(output);
        var middle = result.GetPixel(32, 32);

        // JPEG shifts the exact values, so compare which colour it is nearer to
        // rather than expecting the skin tone back byte for byte.
        Distance(middle, MidBrownSkin).Should().BeLessThan(Distance(middle, CardStock));
    }

    [Test]
    public void DetectPortraitRect_FollowsTheFace_RatherThanAssumingWhereItSits()
    {
        // The photo on the right, where the conventional fallback would NOT look:
        // a rect over on this side can only mean the face was actually found.
        var card = MakeCard(new SKRect(440, 110, 540, 250), MidBrownSkin);
        using var bitmap = SKBitmap.Decode(card);

        var rect = SkiaPortraitCropper.DetectPortraitRect(bitmap);

        rect.MidX.Should().BeGreaterThan(RightOfCentre);
        rect.Contains(490, 180).Should().BeTrue();
    }

    [Test]
    public void DetectPortraitRect_RecognisesDarkSkinTones()
    {
        var card = MakeCard(new SKRect(440, 110, 540, 250), DarkBrownSkin);
        using var bitmap = SKBitmap.Decode(card);

        var rect = SkiaPortraitCropper.DetectPortraitRect(bitmap);

        rect.MidX.Should().BeGreaterThan(RightOfCentre);
        rect.Contains(490, 180).Should().BeTrue();
    }

    [Test]
    public void DetectPortraitRect_ReachesAboveTheFace_ForTheHeadRatherThanTheSkin()
    {
        var card = MakeCard(FaceOnTheLeft, MidBrownSkin);
        using var bitmap = SKBitmap.Decode(card);

        var rect = SkiaPortraitCropper.DetectPortraitRect(bitmap);

        // The detected skin runs 110→250. A portrait wants the hair and forehead
        // above it and only a little of the neck below, so the crop is taller than
        // the skin and its centre sits above the skin's.
        rect.Height.Should().BeGreaterThan(140);
        rect.MidY.Should().BeLessThan(180);
    }

    [Test]
    public void DetectPortraitRect_IsAlwaysASquareInsideTheBitmap()
    {
        // The face jammed into the corner: the square cannot be centred on it
        // without hanging off two edges, so it has to be slid back inside.
        var card = MakeCard(new SKRect(4, 4, 90, 130), MidBrownSkin);
        using var bitmap = SKBitmap.Decode(card);

        var rect = SkiaPortraitCropper.DetectPortraitRect(bitmap);

        rect.Width.Should().Be(rect.Height);
        rect.Left.Should().BeGreaterThanOrEqualTo(0);
        rect.Top.Should().BeGreaterThanOrEqualTo(0);
        rect.Right.Should().BeLessThanOrEqualTo(CardWidth);
        rect.Bottom.Should().BeLessThanOrEqualTo(CardHeight);
    }

    [Test]
    public void DetectPortraitRect_FallsBackToThePhotoCorner_WhenThereIsNoFace()
    {
        // The back of the card: no photo on it at all.
        var card = MakeCard(SKRect.Empty, CardStock);
        using var bitmap = SKBitmap.Decode(card);

        var rect = SkiaPortraitCropper.DetectPortraitRect(bitmap);

        rect.Should().Be(SkiaPortraitCropper.ConventionalPortraitRect(bitmap));
    }

    [Test]
    public void DetectPortraitRect_FallsBack_WhenTheWholeCardReadsAsSkin()
    {
        // A warm beige card fills the frame with skin-coloured pixels. One region
        // covering the whole picture is the card, not a face on it — the size cap
        // is what keeps this from cropping the middle of the document.
        var card = MakeCard(SKRect.Empty, MidBrownSkin);
        using var bitmap = SKBitmap.Decode(card);

        var rect = SkiaPortraitCropper.DetectPortraitRect(bitmap);

        rect.Should().Be(SkiaPortraitCropper.ConventionalPortraitRect(bitmap));
    }

    [Test]
    public void ConventionalPortraitRect_LooksLeftOnACard_AndHeadOnForAPhotoOfAPerson()
    {
        using var card = new SKBitmap(600, 380);
        using var portrait = new SKBitmap(400, 500);

        // A card is held landscape and its photo is in the left-hand third.
        SkiaPortraitCropper.ConventionalPortraitRect(card).MidX.Should().BeLessThan(300);

        // Anything not card-shaped is likelier a picture of the person; crop the
        // middle, high up, where a head is.
        var head = SkiaPortraitCropper.ConventionalPortraitRect(portrait);
        head.MidX.Should().BeInRange(180, 220);
        head.MidY.Should().BeLessThan(250);
    }

    [Test]
    public void CropIdentityPortraitToJpeg_ReturnsNull_OnUndecodableBytes()
    {
        // What a PDF upload looks like to an image decoder — and PDFs are a
        // perfectly ordinary way to hand over a scanned CIN.
        _sut.CropIdentityPortraitToJpeg([1, 2, 3]).Should().BeNull();
    }

    [Test]
    public void CropIdentityPortraitToJpeg_ReturnsNull_OnAnImageTooSmallToCrop()
    {
        using var tiny = new SKBitmap(32, 24);
        using (var canvas = new SKCanvas(tiny))
        {
            canvas.Clear(MidBrownSkin);
        }

        _sut.CropIdentityPortraitToJpeg(Encode(tiny)).Should().BeNull();
    }

    // --- Fixtures -------------------------------------------------------------

    private static readonly SKRect FaceOnTheLeft = new(60, 110, 160, 250);

    // A card-stock rectangle with an oval of `skin` on it. An empty face rect
    // means no photo: the whole card is filled with `skin` instead, which is how
    // the "the background is skin-coloured" and "there is no face" cases are set
    // up.
    private static byte[] MakeCard(SKRect face, SKColor skin)
    {
        using var bitmap = new SKBitmap(CardWidth, CardHeight);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(face.IsEmpty ? skin : CardStock);

            if (!face.IsEmpty)
            {
                using var paint = new SKPaint { Color = skin, IsAntialias = true };
                canvas.DrawOval(face, paint);
            }
        }

        return Encode(bitmap);
    }

    private static byte[] Encode(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        // PNG: a JPEG fixture would blur the oval's edge and blunt the very
        // thresholds under test.
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static double Distance(SKColor a, SKColor b)
    {
        double dr = a.Red - b.Red, dg = a.Green - b.Green, db = a.Blue - b.Blue;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }
}
