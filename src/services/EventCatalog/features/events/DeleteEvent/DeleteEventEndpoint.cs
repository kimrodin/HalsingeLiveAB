using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EventCatalog.Features.Events.DeleteEvent;

[Controller]
[ApiController]
[Route("events")]
public class DeleteEventEndpoint : ControllerBase
{
    private readonly ILogger<DeleteEventEndpoint> _logger;
    private readonly DeleteEventHandler _handler;

    public DeleteEventEndpoint(ILogger<DeleteEventEndpoint> logger, DeleteEventHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }


    // DELETE /event/{id} skickar lokalens id till handlern för borttagning.
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteEvent([FromRoute] Guid id)
    {
        // Avvisa ett tomt lokal-id innan handlern anropas.
        if (id == Guid.Empty)
            return BadRequest("EventId must not be empty.");

        try
        {
            _logger.LogInformation("Deleting event {EventId}", id);

            var request = new DeleteEventRequest(id);
            await _handler.Handle(request);

            // När handlern har slutförts returneras 204 utan svarskropp.
            return NoContent();
        }
        catch (Exception ex)
        {
            // Nuvarande felhantering loggar alla undantag och returnerar 500.
            _logger.LogError(ex, "Failed to delete event {EventId}", id);
            return Problem(
                title: "An error occurred while deleting the event.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}