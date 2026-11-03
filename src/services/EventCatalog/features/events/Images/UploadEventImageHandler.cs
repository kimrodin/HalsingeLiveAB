#nullable enable
using EventCatalog.Data;
using EventCatalog.Shared.Files;
using EventCatalog.Features.Events.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EventCatalog.Features.Events.Images;

public sealed class UploadEventImageHandler(EventCatalogDbContext db, IEventImageStorage storage,
    IOptions<EventImageStorageOptions> options, ILogger<UploadEventImageHandler> logger)
{
    public async Task<EventImageResponse?> Handle(Guid eventId, UploadEventImageRequest request,
        CancellationToken cancellationToken = default)
    {
        EventValidation.ValidateId(eventId, nameof(eventId));
        if (!await db.Events.AnyAsync(e => e.EventId == eventId, cancellationToken)) return null;
        UploadedFile file;
        try { file = await FileUploadReader.ReadAsync(request.File, options.Value.MaxFileSizeBytes, cancellationToken); }
        catch (FileUploadValidationException ex)
        { throw new EventValidationException(new() { ["File"] = [ex.Message] }); }
        var fileName = file.FileName;
        var contentType = EventImageValidation.Validate(request.Name, fileName, file.Bytes);
        await using var buffer = new MemoryStream(file.Bytes, writable: false);
        var old = await db.EventImages.SingleOrDefaultAsync(i => i.EventId == eventId, cancellationToken);
        var storageKey = await storage.SaveAsync(buffer, file.Extension, cancellationToken);
        var image = new EventImage
        {
            EventImageId = Guid.NewGuid(), EventId = eventId, Name = request.Name.Trim(),
            FileName = fileName, ContentType = contentType, StorageKey = storageKey
        };
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            if (old is not null)
            {
                db.EventImages.Remove(old);
                await db.SaveChangesAsync(cancellationToken);
            }
            db.EventImages.Add(image);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await TryDeleteFile(storageKey);
            throw;
        }
        if (old is not null) await TryDeleteFile(old.StorageKey);
        return new(image.EventImageId, eventId, image.Name, fileName, contentType,
            $"/DownloadEventImage/{eventId}/{image.EventImageId}");
    }

    private async Task TryDeleteFile(string key)
    {
        try { await storage.DeleteAsync(key, CancellationToken.None); }
        catch (Exception ex) { logger.LogWarning(ex, "Could not clean up event image {StorageKey}", key); }
    }
}
