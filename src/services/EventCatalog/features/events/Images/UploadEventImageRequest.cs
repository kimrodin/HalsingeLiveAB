namespace EventCatalog.Features.Events.Images;

public sealed class UploadEventImageRequest
{
    public string Name { get; set; } = string.Empty;
    public IFormFile File { get; set; } = null!;
}
