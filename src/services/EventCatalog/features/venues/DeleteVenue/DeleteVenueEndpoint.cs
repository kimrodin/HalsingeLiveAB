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


    // DELETE /venues/{id} skickar lokalens id till handlern för borttagning.
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteVenue([FromRoute] Guid id)
    {
        // Avvisa ett tomt lokal-id innan handlern anropas.
        if (id == Guid.Empty)
            return BadRequest("VenueId must not be empty.");

        try
        {
            _logger.LogInformation("Deleting venue {VenueId}", id);

            var request = new DeleteVenueRequest(id);
            await _handler.Handle(request);

            // När handlern har slutförts returneras 204 utan svarskropp.
            return NoContent();
        }
        catch (Exception ex)
        {
            // Nuvarande felhantering loggar alla undantag och returnerar 500.
            _logger.LogError(ex, "Failed to delete venue {VenueId}", id);
            return Problem(
                title: "An error occurred while deleting the venue.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
