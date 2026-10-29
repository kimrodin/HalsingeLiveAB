namespace EventCatalog.Features.Events.Images;

public sealed class EventImageStorageOptions
{
    public string RootPath { get; set; } = string.Empty;
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}
