namespace EventCatalog.Features.Events.Images;

public record EventImageResponse(Guid EventImageId, Guid EventId, string Name,
    string FileName, string ContentType, string DownloadUrl);
public record EventImageDownload(Stream Content, string FileName, string ContentType);
