namespace EventCatalog.Features.Events.Images;

public static class EventImageValidation
{
    public static string Validate(string name, string fileName, ReadOnlySpan<byte> bytes)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            errors["Name"] = ["An image name of at most 200 characters is required."];
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255)
            errors["File"] = ["A file name of at most 255 characters is required."];
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            ? "image/png"
            : bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF
                ? "image/jpeg" : string.Empty;
        if (!((contentType == "image/png" && extension == ".png")
            || (contentType == "image/jpeg" && extension is ".jpg" or ".jpeg")))
            errors["File"] = ["Choose a PNG or JPEG image with matching file contents."];
        if (errors.Count > 0) throw new EventValidationException(errors);
        return contentType;
    }
}
