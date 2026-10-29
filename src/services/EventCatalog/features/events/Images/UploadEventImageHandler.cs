#nullable enable
using EventCatalog.Data;
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
        var file = request.File;
        var limit = options.Value.MaxFileSizeBytes;
        if (file is null || file.Length <= 0 || file.Length > limit)
            throw new EventValidationException(new() { ["File"] = ["Choose an image within the configured file size limit."] });
        if (!await db.Events.AnyAsync(e => e.EventId == eventId, cancellationToken)) return null;

        // Bound the actual read as well as checking the reported file size.
        await using var input = file.OpenReadStream();
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > limit)
                throw new EventValidationException(new() { ["File"] = ["The image exceeds the file size limit."] });
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        var fileName = Path.GetFileName(file.FileName);
        var contentType = EventImageValidation.Validate(request.Name, fileName, buffer.ToArray());
        buffer.Position = 0;
        var old = await db.EventImages.SingleOrDefaultAsync(i => i.EventId == eventId, cancellationToken);
        var storageKey = await storage.SaveAsync(buffer, Path.GetExtension(fileName), cancellationToken);
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
