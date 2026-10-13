using EventCatalog.Features.Venues.Models;

namespace EventCatalog.Features.Venues;

// Reglerna anropas av handlers, även vid användning utanför HTTP-API:t.
public static class VenueValidation
{
    public static void ValidateVenue(string name, string address, int capacity)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateText(errors, "Name", name, 200);
        ValidateText(errors, "Address", address, 500);
        ValidateCapacity(errors, capacity);
        ThrowIfInvalid(errors);
    }

    public static void ValidateSection(string name, int capacity, SeatingType seatingType)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateText(errors, "Name", name, 100);
        ValidateCapacity(errors, capacity);
        if (!Enum.IsDefined(seatingType))
            errors["SeatingType"] = ["Invalid seating type."];
        ThrowIfInvalid(errors);
    }

    public static void ValidateId(Guid id, string fieldName)
    {
        if (id == Guid.Empty)
            throw new VenueValidationException(new Dictionary<string, string[]>
            {
                [fieldName] = [$"{fieldName} must not be empty."]
            });
    }

    private static void ValidateText(Dictionary<string, string[]> errors, string field, string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors[field] = [$"Please enter a {field.ToLowerInvariant()}."];
        else if (value.Length > maxLength)
            errors[field] = [$"{field} can be at most {maxLength} characters long."];
    }

    private static void ValidateCapacity(Dictionary<string, string[]> errors, int capacity)
    {
        if (capacity <= 0)
            errors["Capacity"] = ["The capacity must be greater than zero."];
    }

    private static void ThrowIfInvalid(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
            throw new VenueValidationException(errors);
    }
}

public sealed class VenueValidationException(Dictionary<string, string[]> errors)
    : Exception("The request contains invalid values.")
{
    public Dictionary<string, string[]> Errors { get; } = errors;
}

public sealed class VenueCapacityConflictException(string message) : Exception(message);
