namespace Booking.Features.Privacy;

public interface IPaymentPrivacyDataClient
{
    Task<IReadOnlyList<PaymentResultOutboxSummaryResponse>> GetResultOutboxAsync(
        Guid bookingId,
        CancellationToken cancellationToken);
}

/// <summary>Exposes non-diagnostic delivery metadata for one payment-result message.</summary>
public sealed record PaymentResultOutboxSummaryResponse(
    Guid MessageId,
    Guid PaymentId,
    Guid BookingId,
    decimal Amount,
    string Currency,
    string Outcome,
    DateTimeOffset? PaidAtUtc,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset? DeliveredAtUtc,
    int AttemptCount);
