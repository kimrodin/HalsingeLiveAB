using Microsoft.Extensions.Options;

namespace EventCatalog.Features.Events.Images;

public sealed class LocalEventImageStorage(IOptions<EventImageStorageOptions> options)
    : IEventImageStorage
{
    private static readonly HashSet<string> AllowedExtensions = [".png", ".jpg", ".jpeg"];

    private readonly string rootPath = ResolveRootPath(options.Value.RootPath);

    public async Task<string> SaveAsync(
        Stream content,
        string extension,
        CancellationToken cancellationToken)
    {
        extension = extension.ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("Unsupported event image extension.");

        Directory.CreateDirectory(rootPath);
        var storageKey = $"{Guid.NewGuid():N}{extension}";
        var destinationPath = ResolvePath(storageKey);

        try
        {
            await using var destination = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            await content.CopyToAsync(destination, cancellationToken);
            return storageKey;
        }
        catch
        {
            if (File.Exists(destinationPath)) File.Delete(destinationPath);
            throw;
        }
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(
            ResolvePath(storageKey),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(storageKey);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string ResolvePath(string storageKey)
    {
        var extension = Path.GetExtension(storageKey).ToLowerInvariant();
        var baseName = Path.GetFileNameWithoutExtension(storageKey);
        if (!AllowedExtensions.Contains(extension)
            || !Guid.TryParseExact(baseName, "N", out _)
            || !string.Equals(Path.GetFileName(storageKey), storageKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid event image storage key.");
        }

        return Path.Combine(rootPath, storageKey);
    }

    private static string ResolveRootPath(string configuredPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, "event-images")
            : configuredPath;
        return Path.GetFullPath(path);
    }
}
