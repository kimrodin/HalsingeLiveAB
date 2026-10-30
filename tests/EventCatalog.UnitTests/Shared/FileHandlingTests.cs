using EventCatalog.Features.Events;
using EventCatalog.Features.Events.Images;
using EventCatalog.Features.Venues;
using EventCatalog.Features.Venues.SeatingMaps;
using EventCatalog.Shared.Files;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace EventCatalog.UnitTests.Shared;

public sealed class FileHandlingTests
{
    [Fact]
    public async Task Reader_IdentifiesFileContentsAndStripsDirectoryFromFileName()
    {
        byte[] bytes = [137, 80, 78, 71, 13, 10, 26, 10];
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", "folder/poster.PNG")
        { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
        var upload = await FileUploadReader.ReadAsync(file, 100, CancellationToken.None);
        Assert.Equal("poster.PNG", upload.FileName);
        Assert.Equal(".png", upload.Extension);
        Assert.Equal("image/png", upload.ContentType);
        Assert.Equal(bytes, upload.Bytes);
    }

    [Fact]
    public async Task Reader_EnforcesActualSizeWhenReportedLengthIsIncorrect()
    {
        var file = new MisreportedFile(new byte[11]);
        await Assert.ThrowsAsync<FileUploadValidationException>(() => FileUploadReader.ReadAsync(file, 10, CancellationToken.None));
        await Assert.ThrowsAsync<FileUploadValidationException>(() => FileUploadReader.ReadAsync(null, 10, CancellationToken.None));
        await Assert.ThrowsAsync<FileUploadValidationException>(() => FileUploadReader.ReadAsync(new MisreportedFile([]), 10, CancellationToken.None));
    }

    [Fact]
    public async Task Reader_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            FileUploadReader.ReadAsync(new MisreportedFile([1, 2, 3]), 10, cancellation.Token));
    }

    [Fact]
    public void FileTypePolicy_AllowsPdfForMapsButNotEventImages()
    {
        var bytes = "%PDF-1.7"u8.ToArray();
        var type = FileContentType.Detect(bytes);
        VenueValidation.ValidateSeatingMap("Map", "map.pdf", bytes.Length, 100, ".pdf", type);
        Assert.Throws<EventValidationException>(() => EventImageValidation.Validate("Poster", "poster.pdf", bytes));
        Assert.False(FileContentType.MatchesExtension(".png", type, "image/png", "application/pdf"));
    }

    [Fact]
    public async Task LocalStorage_RemovesPartialFileAfterFailedWrite()
    {
        var root = Path.Combine(Path.GetTempPath(), "file-storage-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalFileStorage(root, [".png"]);
            await using var stream = new FailingCopyStream();
            await Assert.ThrowsAsync<IOException>(() => storage.SaveAsync(stream, ".png", CancellationToken.None));
            Assert.Empty(Directory.GetFiles(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task StorageAdapters_KeepFilesAndFormatPoliciesSeparate()
    {
        var root = Path.Combine(Path.GetTempPath(), "file-storage-test-" + Guid.NewGuid().ToString("N"));
        var imagesRoot = Path.Combine(root, "images");
        var mapsRoot = Path.Combine(root, "maps");
        try
        {
            var images = new LocalEventImageStorage(Options.Create(new EventImageStorageOptions { RootPath = imagesRoot }));
            var maps = new LocalVenueSeatingMapStorage(Options.Create(new VenueMapStorageOptions { RootPath = mapsRoot }));
            var key = await maps.SaveAsync(new MemoryStream("%PDF-"u8.ToArray()), ".pdf", CancellationToken.None);
            Assert.True(File.Exists(Path.Combine(mapsRoot, key)));
            Assert.False(Directory.Exists(imagesRoot));
            await Assert.ThrowsAsync<InvalidOperationException>(() => images.SaveAsync(new MemoryStream(), ".pdf", CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await maps.OpenReadAsync("../" + key, CancellationToken.None));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    private sealed class FailingCopyStream : MemoryStream
    {
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            await destination.WriteAsync(new byte[] { 1, 2, 3 }, cancellationToken);
            throw new IOException("Simulated read failure.");
        }
    }

    private sealed class MisreportedFile(byte[] bytes) : IFormFile
    {
        public string ContentType => "image/png";
        public string ContentDisposition => string.Empty;
        public IHeaderDictionary Headers => new HeaderDictionary();
        public long Length => 1;
        public string Name => "File";
        public string FileName => "poster.png";
        public Stream OpenReadStream() => new MemoryStream(bytes);
        public void CopyTo(Stream target) => throw new NotSupportedException();
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
