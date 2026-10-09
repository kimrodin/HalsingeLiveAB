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

    // PUT /venues/{id} tar emot lokalens nya uppgifter som JSON.
    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateVenue([FromRoute] Guid id, [FromBody] UpdateVenueRequest request)
    {
        // Avvisa ett tomt lokal-id innan handlern anropas.
        if (id == Guid.Empty)
            return BadRequest("VenueId must not be empty.");

        try
        {
            logger.LogInformation("Updating venue {VenueId}", id);

            // Använd id från URL:en även om requesten innehåller ett annat VenueId.
            var updated = await handler.Handle(request with { VenueId = id });

            // Handlern returnerar false när lokalen saknas, vilket ger 404.
            if (!updated)
            {
                logger.LogWarning("Venue {VenueId} not found for update.", id);
                return NotFound();
            }

            // En lyckad uppdatering ger 204 utan svarskropp.
            return NoContent();
        }
        catch (Exception ex)
        {
            // Logga felet och returnera 500 med ProblemDetails.
            logger.LogError(ex, "Failed to update venue {VenueId}", id);
            return Problem(
                title: "An error occurred while updating the venue.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
