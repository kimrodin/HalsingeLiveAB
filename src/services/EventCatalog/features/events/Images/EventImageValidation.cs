using EventCatalog.Shared.Files;

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
        var contentType = FileContentType.Detect(bytes);
        if (!FileContentType.MatchesExtension(extension, contentType, "image/png", "image/jpeg"))
            errors["File"] = ["Choose a PNG or JPEG image with matching file contents."];
        if (errors.Count > 0) throw new EventValidationException(errors);
        return contentType;
    }
}
