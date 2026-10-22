using Booking.Features.Bookings;

namespace Booking.UnitTests;

public sealed class BookingValidationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void ValidateCreateBooking_AcceptsQuantityWithinAllowedRange(int quantity)
    {
        var request = new CreateBookingRequest(
            Guid.NewGuid(),
            quantity,
            "Test Customer",
            "customer@example.com");

        BookingValidation.ValidateCreateBooking(request);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void ValidateCreateBooking_RejectsQuantityOutsideAllowedRange(int quantity)
    {
        var request = new CreateBookingRequest(
            Guid.NewGuid(),
            quantity,
            "Test Customer",
            "customer@example.com");

        var exception = Assert.Throws<BookingValidationException>(
            () => BookingValidation.ValidateCreateBooking(request));

        Assert.Contains("Quantity", exception.Errors.Keys);
    }
}
