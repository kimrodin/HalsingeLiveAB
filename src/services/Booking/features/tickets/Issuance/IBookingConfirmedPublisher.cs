using HalsingeLive.Contracts.Messaging;

namespace Booking.Features.Tickets.Issuance;

public interface IBookingConfirmedPublisher
{
    Task PublishAsync(BookingConfirmed message, CancellationToken cancellationToken = default);
}
