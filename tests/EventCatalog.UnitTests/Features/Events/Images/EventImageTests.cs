using EventCatalog.Features.Events;
using EventCatalog.Features.Events.Images;
using Microsoft.Extensions.Options;

namespace EventCatalog.UnitTests.Features.Events.Images;

public sealed class EventImageTests
{
    [Fact]
    public void Validation_UsesFileSignatureAndRejectsMismatchedExtensions()
    {
        byte[] png = [137, 80, 78, 71, 13, 10, 26, 10];
        Assert.Equal("image/png", EventImageValidation.Validate("Poster", "poster.png", png));
        Assert.Throws<EventValidationException>(() => EventImageValidation.Validate("Poster", "poster.jpg", png));
        Assert.Throws<EventValidationException>(() => EventImageValidation.Validate("Poster", "poster.png", "%PDF-"u8));
        Assert.Throws<EventValidationException>(() => EventImageValidation.Validate("", "poster.png", png));
    }

    [Fact]
    public async Task LocalStorage_RoundTripsAndRejectsTraversalAndPdf()
    {
        var root = Path.Combine(Path.GetTempPath(), "event-images-test-" + Guid.NewGuid().ToString("N"));
        var storage = new LocalEventImageStorage(Options.Create(new EventImageStorageOptions { RootPath = root }));
        try
        {
            byte[] bytes = [137, 80, 78, 71, 13, 10, 26, 10];
            var key = await storage.SaveAsync(new MemoryStream(bytes), ".PNG", CancellationToken.None);
            Assert.EndsWith(".png", key);
            await using (var content = await storage.OpenReadAsync(key, CancellationToken.None))
            {
                using var read = new MemoryStream();
                await content.CopyToAsync(read);
                Assert.Equal(bytes, read.ToArray());
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() => storage.SaveAsync(new MemoryStream(bytes), ".pdf", CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await storage.OpenReadAsync("../" + key, CancellationToken.None));
            await storage.DeleteAsync(key, CancellationToken.None);
            Assert.False(File.Exists(Path.Combine(root, key)));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
