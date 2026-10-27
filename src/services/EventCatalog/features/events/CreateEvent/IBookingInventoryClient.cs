namespace EventCatalog.Features.Events.CreateEvent;

public interface IBookingInventoryClient
{
    Task EnsureInventoryCreatedAsync(Guid eventId, CancellationToken cancellationToken);
}

public sealed class BookingInventoryProvisioningException(string message, Exception innerException)
    : Exception(message, innerException);
