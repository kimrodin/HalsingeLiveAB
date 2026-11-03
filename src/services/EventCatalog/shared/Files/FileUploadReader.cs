#nullable enable
namespace EventCatalog.Shared.Files;

public sealed record UploadedFile(string FileName, string Extension, string ContentType, byte[] Bytes);
public sealed class FileUploadValidationException(string message) : Exception(message);

public static class FileUploadReader
{
    public static async Task<UploadedFile> ReadAsync(IFormFile? file, long maxFileSizeBytes,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0)
            throw new FileUploadValidationException("Choose a non-empty file.");
        if (file.Length > maxFileSizeBytes)
            throw new FileUploadValidationException("The file exceeds the configured size limit.");

        await using var input = file.OpenReadStream();
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(chunk, cancellationToken)) > 0)
        {
            // Do not trust the length supplied by the caller.
            if (buffer.Length + read > maxFileSizeBytes)
                throw new FileUploadValidationException("The file exceeds the configured size limit.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        if (buffer.Length == 0)
            throw new FileUploadValidationException("Choose a non-empty file.");
        var bytes = buffer.ToArray();
        var fileName = Path.GetFileName(file.FileName);
        return new(fileName, Path.GetExtension(fileName).ToLowerInvariant(), FileContentType.Detect(bytes), bytes);
    }
}
