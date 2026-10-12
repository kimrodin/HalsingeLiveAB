using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EventCatalog.Features.Venues.DeleteSection;

[ApiController]
[Route("venues/{venueId:guid}/sections")]
public class DeleteSectionEndpoint(
    DeleteSectionHandler handler,
    ILogger<DeleteSectionEndpoint> logger) : ControllerBase
{
    [HttpDelete("{sectionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteSection(
        [FromRoute] Guid venueId,
        [FromRoute] Guid sectionId,
        CancellationToken cancellationToken)
    {
        if (venueId == Guid.Empty)
            return BadRequest("VenueId must not be empty.");

        if (sectionId == Guid.Empty)
            return BadRequest("SectionId must not be empty.");

        try
        {
            var deleted = await handler.Handle(
                new DeleteSectionRequest(venueId, sectionId),
                cancellationToken);

            if (!deleted)
                return NotFound();

            return NoContent();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to delete section {SectionId} from venue {VenueId}",
                sectionId,
                venueId);

            return Problem(
                title: "An error occurred while deleting the section.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
