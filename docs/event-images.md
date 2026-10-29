# Event images

Each event can have one uploaded PNG or JPEG poster (maximum 10 MiB by default).
Metadata is stored in `EventImages`; file contents use `IEventImageStorage`, following
the venue seating map storage pattern. Existing events need no image.

In admin, save the event first, then open **Redigera** and use **Evenemangsbild**
to upload, replace or remove its poster. Replacing uses a new image ID and URL;
the old URL stops resolving. Event list/detail responses include a nullable
`imageUrl` and never expose the internal storage key.

The dashboard and customer list, carousel and detail page use this URL. Customer
pages retain the Hälsinge Live brand poster when an image is missing or cannot be loaded.

## API

- `POST /UploadEventImage/{eventId}`: admin-only multipart upload (`Name`, `File`),
  returns image metadata and download URL. Replaces an existing poster.
- `GET /DownloadEventImage/{eventId}/{imageId}`: public inline image response,
  or 404 when the image/event pair is missing.
- `DELETE /DeleteEventImage/{eventId}`: admin-only removal, returns 204 or 404.

PNG/JPEG signatures and matching file extensions are checked on the server;
SVG and PDF are not accepted. Request body limit: 11 MiB including multipart
fields. `EventImageStorage:MaxFileSizeBytes` can lower the default 10 MiB file limit.

## Storage and migration

The `AddEventImages` EF migration creates a unique event foreign key with cascade
delete. Docker Compose mounts `event-images-data` at `/app/uploads/event-images`
and sets `EventImageStorage__RootPath` to that directory. For local .NET runs,
configure `EventImageStorage:RootPath` to a persistent writable directory; otherwise
it defaults to `event-images` under the application base directory.

Ordinary event deletion also removes its poster file. Removing seed data is
blocked while seeded events have images, matching the seating map safeguard.
File deletion failures after a committed database change are logged for cleanup;
filesystem writes and database transactions cannot commit atomically. Back up
both the database and image volume. Apply migrations through the usual deployment
process; Development with `InitializeDatabase=true` applies them at startup.
