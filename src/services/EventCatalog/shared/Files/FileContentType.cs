namespace EventCatalog.Shared.Files;

public static class FileContentType
{
    public static string Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return "image/png";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";
        if (bytes.Length >= 5 && bytes[..5].SequenceEqual("%PDF-"u8))
            return "application/pdf";
        return "application/octet-stream";
    }

    public static bool MatchesExtension(string extension, string contentType, params string[] allowedContentTypes)
    {
        var expected = extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".pdf" => "application/pdf",
            _ => null
        };
        return expected is not null && expected == contentType && allowedContentTypes.Contains(contentType);
    }
}
