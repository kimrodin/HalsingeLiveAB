using EventCatalog.Features.Events;
using EventCatalog.Features.Events.Images;
using EventCatalog.Features.Events.GetEvent;
using EventCatalog.Features.Events.ListEvents;
using EventCatalog.Features.Events.DeleteEvent;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EventCatalog.IntegrationTests.Features.Events;

public sealed class EventImageHandlerTests(EventCatalogDatabaseFixture database)
    : IClassFixture<EventCatalogDatabaseFixture>
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a1ZkAAAAASUVORK5CYII=");

    private static UploadEventImageRequest Request(byte[]? bytes = null) => new()
    {
        Name = "Poster", File = new FormFile(new MemoryStream(bytes ?? Png), 0, (bytes ?? Png).Length, "File", "poster.png")
    };

    private async Task<Guid> Event()
    {
        var venue = await database.CreateVenueAsync();
        var section = await database.CreateSectionAsync(venue.VenueId);
        return await database.CreateEventAsync(venue.VenueId, section.SectionId);
    }

    private static UploadEventImageHandler Uploader(EventCatalog.Data.EventCatalogDbContext db, IEventImageStorage storage, long limit = 10 * 1024 * 1024) =>
        new(db, storage, Options.Create(new EventImageStorageOptions { MaxFileSizeBytes = limit }), NullLogger<UploadEventImageHandler>.Instance);

    [Fact]
    public async Task UploadReplaceDownloadDelete_RoundTripsAndCleansOldFiles()
    {
        var eventId = await Event();
        var storage = new MemoryStorage();
        await using var db = database.CreateContext();
        var first = (await Uploader(db, storage).Handle(eventId, Request()))!;
        Assert.Single(storage.Files);
        var details = await new GetEventHandler(db).Handle(new(eventId));
        Assert.Equal(first.DownloadUrl, details.ImageUrl);
        var list = await new ListEventsHandler(db).Handle(new(EventName: "Concert", PageSize: 100), CancellationToken.None);
        Assert.Equal(first.DownloadUrl, list.Events.Single(e => e.EventId == eventId).ImageUrl);
        var download = await new GetEventImageHandler(db, storage).Handle(eventId, first.EventImageId, CancellationToken.None);
        await using (var content = download!.Content)
        {
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy);
            Assert.Equal(Png, copy.ToArray());
            Assert.Equal("image/png", download.ContentType);
        }
        await Assert.ThrowsAsync<EventValidationException>(() => Uploader(db, storage).Handle(eventId, Request([1, 2, 3])));
        Assert.Equal(first.DownloadUrl, (await new GetEventHandler(db).Handle(new(eventId))).ImageUrl);
        Assert.Single(storage.Files);
        var second = (await Uploader(db, storage).Handle(eventId, Request()))!;
        Assert.NotEqual(first.DownloadUrl, second.DownloadUrl);
        Assert.Single(storage.Files);
        Assert.Equal(1, await db.EventImages.CountAsync(i => i.EventId == eventId));
        Assert.Null(await new GetEventImageHandler(db, storage).Handle(eventId, first.EventImageId, CancellationToken.None));
        Assert.Null(await new GetEventImageHandler(db, storage).Handle(Guid.NewGuid(), second.EventImageId, CancellationToken.None));
        Assert.True(await new DeleteEventImageHandler(db, storage, NullLogger<DeleteEventImageHandler>.Instance).Handle(eventId, CancellationToken.None));
        Assert.Empty(storage.Files);
        Assert.Null((await new GetEventHandler(db).Handle(new(eventId))).ImageUrl);
    }

    [Fact]
    public async Task Upload_InvalidOrOversizedFileAndMissingEvent_DoNotWriteStorage()
    {
        var eventId = await Event();
        var storage = new MemoryStorage();
        await using var db = database.CreateContext();
        await Assert.ThrowsAsync<EventValidationException>(() => Uploader(db, storage).Handle(eventId, Request([1, 2, 3])));
        await Assert.ThrowsAsync<EventValidationException>(() => Uploader(db, storage, 1).Handle(eventId, Request()));
        Assert.Null(await Uploader(db, storage).Handle(Guid.NewGuid(), Request()));
        Assert.Empty(storage.Files);
    }

    [Fact]
    public async Task DeleteEvent_RemovesImageMetadataAndFile()
    {
        var eventId = await Event();
        var storage = new MemoryStorage();
        await using var db = database.CreateContext();
        await Uploader(db, storage).Handle(eventId, Request());
        Assert.True(await new DeleteEventHandler(db, storage).Handle(new(eventId)));
        Assert.Empty(storage.Files);
        Assert.False(await db.EventImages.AnyAsync(i => i.EventId == eventId));
    }

    private sealed class MemoryStorage : IEventImageStorage
    {
        public Dictionary<string, byte[]> Files { get; } = new();
        public async Task<string> SaveAsync(Stream content, string extension, CancellationToken token)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, token);
            var key = Guid.NewGuid().ToString("N") + extension;
            Files.Add(key, buffer.ToArray());
            return key;
        }
        public Task<Stream> OpenReadAsync(string key, CancellationToken token) => Task.FromResult<Stream>(new MemoryStream(Files[key]));
        public Task DeleteAsync(string key, CancellationToken token) { Files.Remove(key); return Task.CompletedTask; }
    }
}
