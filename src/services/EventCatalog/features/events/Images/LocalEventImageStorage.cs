using EventCatalog.Shared.Files;
using Microsoft.Extensions.Options;

namespace EventCatalog.Features.Events.Images;

public sealed class LocalEventImageStorage(IOptions<EventImageStorageOptions> options)
    : LocalFileStorage(
        string.IsNullOrWhiteSpace(options.Value.RootPath)
            ? Path.Combine(AppContext.BaseDirectory, "event-images")
            : options.Value.RootPath,
        [".png", ".jpg", ".jpeg"]), IEventImageStorage;
