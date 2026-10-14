using Microsoft.AspNetCore.Http;

namespace EventCatalog.Features.Venues.SeatingMaps;

public sealed class UploadVenueSeatingMapRequest
{
    public string Name { get; set; } = string.Empty;
    public IFormFile File { get; set; } = null!;
}
