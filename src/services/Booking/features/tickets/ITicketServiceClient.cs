namespace Booking.Features.Tickets;

public interface ITicketServiceClient
{
    Task IssueTicketsAsync(
        BookingConfirmedMessage message,
        CancellationToken cancellationToken = default);
}
