namespace EventCatalog.Shared.Files;

public class LocalFileStorage : IFileStorage
{
    private readonly string rootPath;
    private readonly HashSet<string> allowedExtensions;

    public LocalFileStorage(string rootPath, IEnumerable<string> allowedExtensions)
    {
        this.rootPath = Path.GetFullPath(rootPath);
        this.allowedExtensions = new(allowedExtensions, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<string> SaveAsync(
        Stream content,
        string extension,
        CancellationToken cancellationToken)
    {
        extension = extension.ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            throw new InvalidOperationException("Unsupported file extension.");

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
        if (!allowedExtensions.Contains(extension)
            || !Guid.TryParseExact(baseName, "N", out _)
            || !string.Equals(Path.GetFileName(storageKey), storageKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid file storage key.");
        }

        return Path.Combine(rootPath, storageKey);
    }
}
