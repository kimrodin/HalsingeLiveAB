using Booking.Features.Payments.Gateway;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Features.Privacy;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("privacy/bookings")]
public sealed class PrivacyController(
    IPrivacyOperationsService privacyOperations,
    ILogger<PrivacyController> logger) : ControllerBase
{
    /// <summary>Returns booking-linked data after an administrator has verified the request.</summary>
    [HttpGet("{bookingId:guid}/export")]
    [ProducesResponseType(typeof(PersonalDataSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ExportPersonalData(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await privacyOperations.GetSummaryAsync(bookingId, cancellationToken);
            if (result is not null)
                logger.LogInformation(
                    "Personal data summary exported for booking {BookingId} by admin subject {AdminSubject}.",
                    bookingId,
                    User.FindFirst("sub")?.Value);
            return result is null ? NotFound() : Ok(result);
        }
        catch (Exception exception) when (exception is HttpRequestException or PaymentContractException)
        {
            logger.LogError("Privacy export could not retrieve linked data for booking {BookingId}.", bookingId);
            return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Title = "Linked personal data is temporarily unavailable.",
                Status = StatusCodes.Status502BadGateway
            });
        }
    }

    /// <summary>Removes direct identifiers while retaining linked booking and transaction records.</summary>
    [HttpDelete("{bookingId:guid}/personal-data")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PseudonymizePersonalData(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var changed = await privacyOperations.PseudonymizeAsync(bookingId, cancellationToken);
        if (changed)
            logger.LogInformation(
                "Personal data pseudonymized for booking {BookingId} by admin subject {AdminSubject}.",
                bookingId,
                User.FindFirst("sub")?.Value);
        return changed ? NoContent() : NotFound();
    }

    /// <summary>Previews bookings eligible for the two-year pseudonymization job.</summary>
    [HttpGet("retention-preview")]
    [ProducesResponseType(typeof(RetentionPreviewResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RetentionPreviewResponse>> GetRetentionPreview(
        CancellationToken cancellationToken) =>
        Ok(await privacyOperations.GetRetentionPreviewAsync(cancellationToken));
}
