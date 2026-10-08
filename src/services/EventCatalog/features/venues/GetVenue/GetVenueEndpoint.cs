using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EventCatalog.Features.Venues.GetVenue;

[Controller]
[ApiController]
[Route("venues")]
public class GetVenueEndpoint : ControllerBase
{
    private readonly ILogger<GetVenueEndpoint> _logger;
    private readonly GetVenueHandler _handler;

    public GetVenueEndpoint(ILogger<GetVenueEndpoint> logger, GetVenueHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }

    // GET: /venues/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetVenueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetVenue([FromRoute] Guid id)
    {
        if (id == Guid.Empty)
            return BadRequest("VenueId must not be empty.");

        try
        {
            _logger.LogInformation("Retrieving venue {VenueId}", id);

            var venue = await _handler.Handle(new GetVenueRequest(id));

            if (venue is null)
                return NotFound();

            return Ok(venue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve venue {VenueId}", id);
            return Problem(
                title: "An error occurred while retrieving the venue.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
