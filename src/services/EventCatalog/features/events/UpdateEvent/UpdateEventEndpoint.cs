using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EventCatalog.Features.Events.UpdateEvent;

[ApiController]
[Route("events")]
public class UpdateEventEndpoint : ControllerBase
{
    private readonly ILogger<UpdateEventEndpoint> logger;
    private readonly UpdateEventHandler handler;

    public UpdateEventEndpoint(ILogger<UpdateEventEndpoint> logger, UpdateEventHandler handler)
    {
        this.logger = logger;
        this.handler = handler;
    }

    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateVenue([FromRoute] Guid id, [FromBody] UpdateEventRequest request)
    {
        if (id == Guid.Empty)
            return BadRequest("EventId must not be empty.");

        try
        {
            logger.LogInformation("Updating event {EventId}", id);

            var updated = await handler.Handle(request with { EventId = id });

            if (!updated)
            {
                logger.LogWarning("Event {EventId} not found for update.", id);
                return NotFound();
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update event {EventId}", id);
            return Problem(
                title: "An error occurred while updating the event.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}

    // TODO: Kräv administratörsbehörighet enligt projektets behörighetsmodell.
    // TODO: Skicka vidare CancellationToken när handlern stöder det.

