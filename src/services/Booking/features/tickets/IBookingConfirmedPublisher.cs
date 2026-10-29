using HalsingeLive.Contracts.Messaging;

namespace Booking.Features.Tickets;

public interface IBookingConfirmedPublisher
{
    Task PublishAsync(BookingConfirmedV1 message, CancellationToken cancellationToken = default);
}
