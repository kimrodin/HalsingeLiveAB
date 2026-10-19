namespace EventCatalog.Features.Venues.SeatingMaps;

public record VenueSeatingMapResponse(
    Guid VenueSeatingMapId,
    Guid VenueId,
    string Name,
    string FileName,
    string ContentType,
    string DownloadUrl);

public record VenueSeatingMapDownload(Stream Content, string FileName, string ContentType);
