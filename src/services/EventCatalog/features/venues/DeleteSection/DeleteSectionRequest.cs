namespace EventCatalog.Features.Venues.DeleteSection;

public record DeleteSectionRequest(
    Guid VenueId,
    Guid SectionId);
