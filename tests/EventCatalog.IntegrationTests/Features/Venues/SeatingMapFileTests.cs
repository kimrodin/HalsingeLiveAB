using EventCatalog.Features.Venues;
using EventCatalog.Features.Venues.SeatingMaps;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EventCatalog.IntegrationTests.Features.Venues;

public sealed class SeatingMapFileTests(EventCatalogDatabaseFixture database)
    : IClassFixture<EventCatalogDatabaseFixture>
{
    [Fact]
    public async Task PdfUploadDownloadDelete_UsesSharedStorageAndKeepsVenueMetadata()
    {
        var venue = await database.CreateVenueAsync();
        var root = Path.Combine(Path.GetTempPath(), "map-files-test-" + Guid.NewGuid().ToString("N"));
        var options = Options.Create(new VenueMapStorageOptions { RootPath = root });
        var storage = new LocalVenueSeatingMapStorage(options);
        byte[] bytes = "%PDF-1.7 test"u8.ToArray();
        await using var db = database.CreateContext();
        try
        {
            var uploaded = await new UploadVenueSeatingMapHandler(db, storage, options).Handle(venue.VenueId,
                new() { Name = "Floor", File = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", "floor.pdf") });
            Assert.NotNull(uploaded);
            Assert.Equal("application/pdf", uploaded.ContentType);
            Assert.Single(Directory.GetFiles(root));
            var download = await new GetVenueSeatingMapHandler(db, storage).Handle(venue.VenueId, uploaded.VenueSeatingMapId);
            await using (var content = download!.Content)
            {
                using var buffer = new MemoryStream();
                await content.CopyToAsync(buffer);
                Assert.Equal(bytes, buffer.ToArray());
            }
            Assert.True(await new DeleteVenueSeatingMapHandler(db, storage).Handle(venue.VenueId, uploaded.VenueSeatingMapId));
            Assert.Empty(Directory.GetFiles(root));
            Assert.False(await db.VenueSeatingMaps.AnyAsync(m => m.VenueSeatingMapId == uploaded.VenueSeatingMapId));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task InvalidUpload_DoesNotWriteFileOrMetadata()
    {
        var venue = await database.CreateVenueAsync();
        var root = Path.Combine(Path.GetTempPath(), "map-files-test-" + Guid.NewGuid().ToString("N"));
        var options = Options.Create(new VenueMapStorageOptions { RootPath = root, MaxFileSizeBytes = 10 });
        var storage = new LocalVenueSeatingMapStorage(options);
        await using var db = database.CreateContext();
        var handler = new UploadVenueSeatingMapHandler(db, storage, options);
        foreach (var bytes in new[] { new byte[11], new byte[] { 1, 2, 3 } })
        {
            await Assert.ThrowsAsync<VenueValidationException>(() => handler.Handle(venue.VenueId,
                new() { Name = "Map", File = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", "map.png") }));
        }
        Assert.False(Directory.Exists(root));
        Assert.False(await db.VenueSeatingMaps.AnyAsync(m => m.VenueId == venue.VenueId));
    }
}
