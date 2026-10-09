using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EventCatalog.Features.Venues.DeleteVenue;

[Controller]
[ApiController]
[Route("venues")]
public class DeleteVenueEndpoint : ControllerBase
{
    private readonly ILogger<DeleteVenueEndpoint> _logger;
    private readonly DeleteVenueHandler _handler;

    public DeleteVenueEndpoint(ILogger<DeleteVenueEndpoint> logger, DeleteVenueHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }


    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteVenue([FromRoute] Guid id)
    {
        if (id == Guid.Empty)
            return BadRequest("VenueId must not be empty.");

        try
        {
            _logger.LogInformation("Deleting venue {VenueId}", id);

            var request = new DeleteVenueRequest(id);
            await _handler.Handle(request);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete venue {VenueId}", id);
            return Problem(
                title: "An error occurred while deleting the venue.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
    // TODO: Registrera DELETE /venues/{id:guid} och bind id till request.
    // TODO: Kräv administratörsbehörighet enligt projektets behörighetsmodell.
    // TODO: Anropa handlern och returnera 204 No Content när lokalen har tagits bort.
    // TODO: Returnera 404 om lokalen saknas och 409 om den används och därför inte får tas bort.
    // TODO: Dokumentera svaren i OpenAPI och skicka vidare CancellationToken.

