using Microsoft.Extensions.Logging;
using RemSolution.Application.Common.Interfaces;
using RemSolution.Domain.Entities;
using RemSolution.Domain.Enums;

namespace RemSolution.Application.Common.Imaging;

/// <summary>
/// Turns a client's CIN image into the small square portrait shown beside their
/// name, as a <see cref="StoredFile"/> like any other file the app keeps.
/// <para>
/// A concrete class rather than an interface: it has one implementation by
/// definition (the seams worth swapping are already behind
/// <see cref="IPortraitCropper"/> and <see cref="IStoredFileService"/>), and its
/// job is only to hold the two callers' shared steps in one place — the upload
/// that has the bytes in hand, and the re-derivation that has to read them back.
/// </para>
/// <para>
/// Never throws on a picture it cannot use. A portrait is a nicety derived from
/// the document, so "no face found" and "the file is a PDF" both return null and
/// leave the caller's real work — storing the document — untouched.
/// </para>
/// </summary>
public sealed class ClientPortraitFactory
{
    // Big enough for a comfortable avatar on a high-density screen, small enough
    // that a page of 50 clients costs a few hundred kilobytes of images.
    private const int PortraitPx = 256;

    private readonly IStoredFileService _storedFiles;
    private readonly IPortraitCropper _cropper;
    private readonly IFileStorage _storage;
    private readonly ILogger<ClientPortraitFactory> _logger;

    public ClientPortraitFactory(
        IStoredFileService storedFiles,
        IPortraitCropper cropper,
        IFileStorage storage,
        ILogger<ClientPortraitFactory> logger)
    {
        _storedFiles = storedFiles;
        _cropper = cropper;
        _storage = storage;
        _logger = logger;
    }

    /// <summary>
    /// Crops the head out of <paramref name="cinBytes"/> and adds the resulting
    /// portrait as a tracked <see cref="StoredFile"/>, or returns null when the
    /// bytes hold no usable picture. Does NOT save: the caller commits it
    /// together with the client's FK, as it does for the document itself.
    /// </summary>
    public async Task<StoredFile?> TryCreateAsync(
        int agencyId, int clientId, byte[] cinBytes, CancellationToken cancellationToken = default)
    {
        byte[]? portrait;
        try
        {
            portrait = _cropper.CropIdentityPortraitToJpeg(cinBytes, PortraitPx);
        }
        catch (Exception ex)
        {
            // Cropping is a best effort on bytes a user chose; a surprise in the
            // imaging library must not cost them their document upload.
            _logger.LogWarning(ex,
                "Could not crop a portrait out of the CIN image of client {ClientId}", clientId);
            return null;
        }

        if (portrait is null)
        {
            _logger.LogInformation(
                "No portrait could be located on the CIN image of client {ClientId}", clientId);
            return null;
        }

        var relativePath = $"agencies/{agencyId}/clients/{clientId}/portrait-{Guid.NewGuid():N}.jpg";

        using var content = new MemoryStream(portrait, writable: false);

        return await _storedFiles.CreateAsync(
            content, "portrait.jpg", "image/jpeg", DocumentType.ClientPortrait,
            relativePath, cancellationToken);
    }

    /// <summary>
    /// The same, for a client whose CIN image is already stored: reads it back out
    /// of storage and re-derives the portrait. Returns null when the client has no
    /// CIN image, its bytes have gone missing, or no face was found on it.
    /// </summary>
    public async Task<StoredFile?> TryCreateFromStoredCinAsync(
        Client client, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        var url = client.CINFile?.Url;
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            await using var source = await _storage.OpenReadAsync(url, cancellationToken);
            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, cancellationToken);
            bytes = buffer.ToArray();
        }
        catch (Exception ex)
        {
            // The row still points at a file the storage no longer has (restored
            // database, moved container). Nothing to re-derive from.
            _logger.LogWarning(ex,
                "Could not read the stored CIN image of client {ClientId} back for cropping", client.Id);
            return null;
        }

        return await TryCreateAsync(client.AgencyId, client.Id, bytes, cancellationToken);
    }
}
