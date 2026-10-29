namespace EventCatalog.Features.Events.Models;

public sealed class EventImage
{
    public Guid EventImageId { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
}
