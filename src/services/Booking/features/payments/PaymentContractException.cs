using System.Net;

namespace Booking.Features.Payments;

// A rejected request or malformed response is distinct from an unknown transport outcome.
// It does not prove that payment failed, so reconciliation must remain open.
public sealed class PaymentContractException(
    string message,
    HttpStatusCode? statusCode = null,
    Exception innerException = null) : Exception(message, innerException)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
}
