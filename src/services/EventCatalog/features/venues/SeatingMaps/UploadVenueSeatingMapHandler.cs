#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using EventCatalog.Data;
using EventCatalog.Features.Venues.Models;

namespace EventCatalog.Features.Venues.SeatingMaps;

public sealed class UploadVenueSeatingMapHandler(
    EventCatalogDbContext db,
    IVenueSeatingMapStorage storage,
    IOptions<VenueMapStorageOptions> options)
{
    public async Task<VenueSeatingMapResponse?> Handle(Guid venueId, UploadVenueSeatingMapRequest request,
        CancellationToken cancellationToken = default)
    {
        VenueValidation.ValidateId(venueId, nameof(venueId));
        var file = request.File;
        if (file is null)
        {
            VenueValidation.ValidateSeatingMap(request.Name, "", 0, options.Value.MaxFileSizeBytes, "", "");
            throw new InvalidOperationException("Validation should reject a missing file.");
        }
        if (file.Length > options.Value.MaxFileSizeBytes)
            VenueValidation.ValidateSeatingMap(request.Name, Path.GetFileName(file.FileName), file.Length,
                options.Value.MaxFileSizeBytes, Path.GetExtension(file.FileName), "");
        if (!await db.Venues.AnyAsync(v => v.VenueId == venueId, cancellationToken)) return null;

        var fileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var contentType = DetectContentType(bytes);
        VenueValidation.ValidateSeatingMap(request.Name, fileName, bytes.LongLength,
            options.Value.MaxFileSizeBytes, extension, contentType);

        await using var fileContent = new MemoryStream(bytes, writable: false);
        var storageKey = await storage.SaveAsync(fileContent, extension, cancellationToken);
        try
        {
            var map = new VenueSeatingMap
            {
                VenueSeatingMapId = Guid.NewGuid(), VenueId = venueId, Name = request.Name.Trim(),
                FileName = fileName, StorageKey = storageKey, ContentType = contentType
            };
            db.VenueSeatingMaps.Add(map);
            await db.SaveChangesAsync(cancellationToken);
            return new(map.VenueSeatingMapId, map.VenueId, map.Name, map.FileName, map.ContentType, string.Empty);
        }
        catch
        {
            try { await storage.DeleteAsync(storageKey, CancellationToken.None); }
            catch { /* Preserve the original database/storage error. */ }
            throw;
        }
    }

    private static string DetectContentType(byte[] bytes)
    {
        ReadOnlySpan<byte> data = bytes;
        if (data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return "image/jpeg";
        if (data.Length >= 5 && data[..5].SequenceEqual("%PDF-"u8)) return "application/pdf";
        return "application/octet-stream";
    }
}
