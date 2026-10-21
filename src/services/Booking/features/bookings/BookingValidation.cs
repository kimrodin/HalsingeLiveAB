using Booking.Features.Bookings.Models;
using Booking.Features.EventInventory.CreateEventInventory;

namespace Booking.Features.Bookings;

// Called by handlers/services so the rules also apply outside HTTP controllers.
public static class BookingValidation
{
    private const decimal MaximumStoredMoney = 9_999_999_999.99m;
    private const int MaximumCustomerIdLength = 255;

    public static void ValidateCreateBooking(CreateBookingRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["Request"] = ["A booking request is required."];
            ThrowIfInvalid(errors);
            return;
        }

        ValidateId(errors, request.EventId, "EventId");
        ValidateId(errors, request.SectionId, "SectionId");
        if (request.Quantity <= 0)
            errors["Quantity"] = ["Quantity must be greater than zero."];

        if (string.IsNullOrWhiteSpace(request.CustomerId))
            errors["CustomerId"] = ["CustomerId is required."];
        else if (request.CustomerId.Trim().Length > MaximumCustomerIdLength)
            errors["CustomerId"] = [$"CustomerId can be at most {MaximumCustomerIdLength} characters long."];

        ThrowIfInvalid(errors);
    }

    public static void ValidateCreateInventory(Guid eventId, CreateEventInventoryRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["Request"] = ["An inventory request is required."];
            ThrowIfInvalid(errors);
            return;
        }

        ValidateId(errors, eventId, "EventId");
        ValidateId(errors, request.SectionId, "SectionId");

        if (request.TotalQuantity <= 0)
            errors["TotalQuantity"] = ["TotalQuantity must be greater than zero."];

        ValidateMoney(errors, "Price", request.Price);
        ThrowIfInvalid(errors);
    }

    public static void ValidatePriceAndTotal(decimal unitPrice, int quantity)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateMoney(errors, "Price", unitPrice);

        if (quantity > 0 && unitPrice >= 0 && unitPrice * quantity > MaximumStoredMoney)
            errors["TotalPrice"] = ["The booking total exceeds the maximum supported amount."];

        ThrowIfInvalid(errors);
    }

    public static void ValidateId(Guid id, string fieldName)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateId(errors, id, fieldName);
        ThrowIfInvalid(errors);
    }

    private static void ValidateId(Dictionary<string, string[]> errors, Guid id, string fieldName)
    {
        if (id == Guid.Empty)
            errors[fieldName] = [$"{fieldName} must not be empty."];
    }

    private static void ValidateMoney(Dictionary<string, string[]> errors, string fieldName, decimal value)
    {
        if (value < 0)
            errors[fieldName] = [$"{fieldName} cannot be negative."];
        else if (value > MaximumStoredMoney)
            errors[fieldName] = [$"{fieldName} exceeds the maximum supported amount."];
        else if (decimal.Round(value, 2) != value)
            errors[fieldName] = [$"{fieldName} can have at most two decimal places."];
    }

    private static void ThrowIfInvalid(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
            throw new BookingValidationException(errors);
    }
}

public sealed class BookingValidationException(Dictionary<string, string[]> errors)
    : Exception("The booking request contains invalid values.")
{
    public Dictionary<string, string[]> Errors { get; } = errors;
}
