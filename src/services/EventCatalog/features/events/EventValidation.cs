using EventCatalog.Features.Events.CreateEvent;
using EventCatalog.Features.Events.UpdateEvent;

namespace EventCatalog.Features.Events;

// Called by handlers so validation also applies when they are used outside HTTP.
public static class EventValidation
{
    public static void ValidateCreateEvent(CreateEventRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["Request"] = ["An event request is required."];
            ThrowIfInvalid(errors);
            return;
        }

        ValidateText(errors, "Name", request.Name, 200);
        ValidateText(errors, "Description", request.Description, 2000);

        if (request.VenueId is null || request.VenueId == Guid.Empty)
            errors["VenueId"] = ["A valid VenueId is required."];
        if (request.StartDate is null || request.StartDate == default(DateTime))
            errors["StartDate"] = ["StartDate is required."];
        if (request.EndDate is null || request.EndDate == default(DateTime))
            errors["EndDate"] = ["EndDate is required."];
        if (request.OnSaleFrom is null || request.OnSaleFrom == default(DateTime))
            errors["OnSaleFrom"] = ["OnSaleFrom is required."];

        if (request.StartDate is not null && request.EndDate is not null
            && request.StartDate >= request.EndDate)
        {
            errors["EndDate"] = ["EndDate must be later than StartDate."];
        }

        if (request.StartDate is not null && request.OnSaleFrom is not null
            && request.OnSaleFrom > request.StartDate)
        {
            errors["OnSaleFrom"] = ["OnSaleFrom must be on or before StartDate."];
        }

        ValidateSectionPrices(
            errors,
            request.SectionPrices?.Where(price => price is not null).Select(price => price.SectionId),
            request.SectionPrices?.Where(price => price is not null).Select(price => price.Price),
            request.SectionPrices is null || request.SectionPrices.Any(price => price is null));

        ThrowIfInvalid(errors);
    }

    public static void ValidateUpdateEvent(UpdateEventRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["Request"] = ["An event update request is required."];
            ThrowIfInvalid(errors);
            return;
        }

        ValidateText(errors, "Name", request.Name, 200);
        ValidateText(errors, "Description", request.Description, 2000);

        if (request.VenueId == Guid.Empty)
            errors["VenueId"] = ["A valid VenueId is required."];
        if (request.StartDate == default)
            errors["StartDate"] = ["StartDate is required."];
        if (request.EndDate == default)
            errors["EndDate"] = ["EndDate is required."];
        if (request.OnSaleFrom == default)
            errors["OnSaleFrom"] = ["OnSaleFrom is required."];
        if (request.StartDate != default && request.EndDate != default
            && request.StartDate >= request.EndDate)
        {
            errors["EndDate"] = ["EndDate must be later than StartDate."];
        }
        if (request.StartDate != default && request.OnSaleFrom != default
            && request.OnSaleFrom > request.StartDate)
        {
            errors["OnSaleFrom"] = ["OnSaleFrom must be on or before StartDate."];
        }

        ValidateSectionPrices(
            errors,
            request.SectionPrices?.Where(price => price is not null).Select(price => price.SectionId),
            request.SectionPrices?.Where(price => price is not null).Select(price => price.Price),
            request.SectionPrices is null || request.SectionPrices.Any(price => price is null));

        var removeIds = request.SectionIdsToRemove ?? Array.Empty<Guid>();
        if (removeIds.Any(id => id == Guid.Empty))
            errors["SectionIdsToRemove"] = ["SectionIdsToRemove cannot contain an empty ID."];
        else if (removeIds.Distinct().Count() != removeIds.Count)
            errors["SectionIdsToRemove"] = ["SectionIdsToRemove must contain unique IDs."];

        ThrowIfInvalid(errors);
    }

    public static void ValidateId(Guid id, string fieldName)
    {
        if (id == Guid.Empty)
            ThrowIfInvalid(new Dictionary<string, string[]>
            {
                [fieldName] = [$"{fieldName} must not be empty."]
            });
    }

    public static void ValidateSectionPricesBelongToVenue(bool allBelongToVenue)
    {
        if (!allBelongToVenue)
            ThrowIfInvalid(new Dictionary<string, string[]>
            {
                ["SectionPrices"] = ["Every priced section must belong to the event's venue."]
            });
    }

    public static void ValidateRemovedSectionPrices(
        IEnumerable<Guid> requestedIds,
        IReadOnlySet<Guid> linkedIds)
    {
        if (requestedIds.Any(id => !linkedIds.Contains(id)))
            ThrowIfInvalid(new Dictionary<string, string[]>
            {
                ["SectionIdsToRemove"] = ["Every section to remove must be linked to this event."]
            });
    }

    public static void ValidateNoSectionPriceIsUpdatedAndRemoved(
        IEnumerable<Guid> updatedIds,
        IEnumerable<Guid> removedIds)
    {
        if (updatedIds.Intersect(removedIds).Any())
            ThrowIfInvalid(new Dictionary<string, string[]>
            {
                ["SectionPrices"] = ["A section price cannot be updated and removed in the same request."]
            });
    }

    private static void ValidateText(
        Dictionary<string, string[]> errors,
        string field,
        string value,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors[field] = [$"Please enter a {field.ToLowerInvariant()}."];
        else if (value.Length > maxLength)
            errors[field] = [$"{field} can be at most {maxLength} characters long."];
    }

    private static void ValidateSectionPrices(
        Dictionary<string, string[]> errors,
        IEnumerable<Guid?> sectionIds,
        IEnumerable<decimal?> prices,
        bool collectionIsNullOrContainsNullEntry)
    {
        if (collectionIsNullOrContainsNullEntry || sectionIds is null || prices is null)
        {
            errors["SectionPrices"] = ["Every section price must include a section ID and a price."];
            return;
        }

        var ids = sectionIds.ToArray();
        var values = prices.ToArray();
        if (ids.Length == 0)
        {
            errors["SectionPrices"] = ["At least one section price is required."];
            return;
        }

        if (ids.Any(id => id is null || id == Guid.Empty))
            errors["SectionPrices"] = ["Every section price must include a valid section ID."];
        if (values.Any(price => price is null || price < 0))
            errors["SectionPrices"] = ["Every section price must be zero or greater."];

        var validIds = ids.Where(id => id is not null && id != Guid.Empty).Select(id => id!.Value);
        if (validIds.Distinct().Count() != validIds.Count())
            errors["SectionPrices"] = ["Each section can only have one price per event."];
    }

    private static void ThrowIfInvalid(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
            throw new EventValidationException(errors);
    }
}

public sealed class EventValidationException(Dictionary<string, string[]> errors)
    : Exception("The event request contains invalid values.")
{
    public Dictionary<string, string[]> Errors { get; } = errors;
}
