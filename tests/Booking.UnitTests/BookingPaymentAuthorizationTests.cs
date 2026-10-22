using Booking.Features.Bookings;
using Microsoft.AspNetCore.Mvc;

namespace Booking.UnitTests;

public sealed class BookingPaymentAuthorizationTests
{
    [Fact]
    public void ConfirmBooking_DirectClientRequestIsRejected()
    {
        var controller = new BookingsController(null!, null!);

        var result = Assert.IsType<ObjectResult>(controller.ConfirmBooking(Guid.NewGuid()));

        Assert.Equal(403, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(403, problem.Status);
    }
}
