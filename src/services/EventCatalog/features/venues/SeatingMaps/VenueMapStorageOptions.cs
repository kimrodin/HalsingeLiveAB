namespace EventCatalog.Features.Venues.SeatingMaps;

public sealed class VenueMapStorageOptions
{
    public string RootPath { get; set; } = string.Empty;
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}
