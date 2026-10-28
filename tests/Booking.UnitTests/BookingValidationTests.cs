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

    [Fact]
    public void ValidateCreateBooking_RejectsEmptyInventoryId()
    {
        var request = new CreateBookingRequest(Guid.Empty, 1, "Test Customer", "customer@example.com");

        var exception = Assert.Throws<BookingValidationException>(
            () => BookingValidation.ValidateCreateBooking(request));

        Assert.Contains("InventoryId", exception.Errors.Keys);
    }

    [Theory]
    [InlineData("", "customer@example.com", "CustomerName")]
    [InlineData("Test Customer", "not-an-email", "CustomerEmail")]
    [InlineData(" Test Customer ", " customer@example.com ", "")]
    public void ValidateCreateBooking_ValidatesCustomerDetails(string name, string email, string expectedError)
    {
        var request = new CreateBookingRequest(Guid.NewGuid(), 1, name, email);

        if (expectedError.Length == 0)
        {
            BookingValidation.ValidateCreateBooking(request);
            return;
        }

        var exception = Assert.Throws<BookingValidationException>(
            () => BookingValidation.ValidateCreateBooking(request));

        Assert.Contains(expectedError, exception.Errors.Keys);
    }

    [Fact]
    public void ValidatePriceAndTotal_RejectsUnsupportedAmounts()
    {
        foreach (var (unitPrice, quantity, expectedError) in new[]
        {
            (-1.00m, 1, "Price"),
            (1.001m, 1, "Price"),
            (6000000000.00m, 2, "TotalPrice")
        })
        {
            var exception = Assert.Throws<BookingValidationException>(
                () => BookingValidation.ValidatePriceAndTotal(unitPrice, quantity));

            Assert.Contains(expectedError, exception.Errors.Keys);
        }
    }
}
