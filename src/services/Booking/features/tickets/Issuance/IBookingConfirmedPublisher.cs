using HalsingeLive.Contracts.Messaging;

namespace Booking.Features.Tickets.Issuance;

public interface IBookingConfirmedPublisher
{
    Task PublishAsync(BookingConfirmedV1 message, CancellationToken cancellationToken = default);
}
