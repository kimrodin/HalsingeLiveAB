namespace EventCatalog.Features.Venues.Models;

public class VenueSeatingMap
{
    public Guid Id { get; set; }
    public Guid VenueId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;

    // Nyckeln till bilden eller PDF-filen i fillagringen.
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
}
