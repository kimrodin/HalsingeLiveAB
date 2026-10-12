using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EventCatalog.Features.Venues.CreateSection;

[ApiController]
[Route("venues/{venueId:guid}/sections")]
public class CreateSectionEndpoint(
    CreateSectionHandler handler,
    ILogger<CreateSectionEndpoint> logger) : ControllerBase
{
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(CreateSectionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateSection(
        [FromRoute] Guid venueId,
        [FromBody] CreateSectionRequest request,
        CancellationToken cancellationToken)
    {
        if (venueId == Guid.Empty)
            return BadRequest("VenueId must not be empty.");

        try
        {
            var response = await handler.Handle(venueId, request, cancellationToken);
            if (response is null)
                return NotFound();

            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create section for venue {VenueId}", venueId);
            return Problem(
                title: "An error occurred while creating the section.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
