using EventCatalog.Shared.Files;
using Microsoft.Extensions.Options;

namespace EventCatalog.Features.Venues.SeatingMaps;

public sealed class LocalVenueSeatingMapStorage(IOptions<VenueMapStorageOptions> options)
    : LocalFileStorage(
        string.IsNullOrWhiteSpace(options.Value.RootPath)
            ? Path.Combine(AppContext.BaseDirectory, "venue-maps")
            : options.Value.RootPath,
        [".png", ".jpg", ".jpeg", ".pdf"]), IVenueSeatingMapStorage;
