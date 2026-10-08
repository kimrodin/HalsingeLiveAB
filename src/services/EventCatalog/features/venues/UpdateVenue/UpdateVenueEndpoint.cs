using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EventCatalog.Features.Venues.UpdateVenue;

[ApiController]
[Route("venues")]
public class UpdateVenueEndpoint : ControllerBase
{
    private readonly ILogger<UpdateVenueEndpoint> logger;
    private readonly UpdateVenueHandler handler;

    public UpdateVenueEndpoint(ILogger<UpdateVenueEndpoint> logger, UpdateVenueHandler handler)
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
    public async Task<IActionResult> UpdateVenue([FromRoute] Guid id, [FromBody] UpdateVenueRequest request)
    {
        if (id == Guid.Empty)
            return BadRequest("VenueId must not be empty.");

        try
        {
            logger.LogInformation("Updating venue {VenueId}", id);

            var updated = await handler.Handle(request with { VenueId = id });

            if (!updated)
            {
                logger.LogWarning("Venue {VenueId} not found for update.", id);
                return NotFound();
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update venue {VenueId}", id);
            return Problem(
                title: "An error occurred while updating the venue.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}

    // TODO: Kräv administratörsbehörighet enligt projektets behörighetsmodell.
    // TODO: Skicka vidare CancellationToken när handlern stöder det.

