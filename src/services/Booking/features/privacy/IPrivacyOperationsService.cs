namespace Booking.Features.Privacy;

public interface IPrivacyOperationsService
{
    Task<PersonalDataSummaryResponse> GetSummaryAsync(
        Guid bookingId,
        CancellationToken cancellationToken);

    Task<bool> PseudonymizeAsync(Guid bookingId, CancellationToken cancellationToken);

    Task<RetentionPreviewResponse> GetRetentionPreviewAsync(CancellationToken cancellationToken);

    Task<int> PseudonymizeExpiredAsync(CancellationToken cancellationToken);
}
