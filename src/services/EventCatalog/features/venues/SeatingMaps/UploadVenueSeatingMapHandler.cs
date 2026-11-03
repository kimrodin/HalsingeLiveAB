#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using EventCatalog.Data;
using EventCatalog.Shared.Files;
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
        if (!await db.Venues.AnyAsync(v => v.VenueId == venueId, cancellationToken)) return null;
        UploadedFile file;
        try { file = await FileUploadReader.ReadAsync(request.File, options.Value.MaxFileSizeBytes, cancellationToken); }
        catch (FileUploadValidationException ex)
        { throw new VenueValidationException(new() { ["File"] = [ex.Message] }); }
        var fileName = file.FileName;
        var extension = file.Extension;
        var contentType = file.ContentType;
        VenueValidation.ValidateSeatingMap(request.Name, fileName, file.Bytes.LongLength,
            options.Value.MaxFileSizeBytes, extension, contentType);
        await using var fileContent = new MemoryStream(file.Bytes, writable: false);
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
}
