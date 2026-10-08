using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EventCatalog.Features.Events.GetEvent;

[Controller]
[ApiController]
[Route("events")]
public class GetEventEndpoint : ControllerBase
{
    private readonly ILogger<GetEventEndpoint> _logger;
    private readonly GetEventHandler _handler;

    public GetEventEndpoint(ILogger<GetEventEndpoint> logger, GetEventHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }

    // GET: /events/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEvent([FromRoute] Guid id)
    {
        if (id == Guid.Empty)
            return BadRequest("EventId must not be empty.");

        try
        {
            _logger.LogInformation("Retrieving event {EventId}", id);

            var response = await _handler.Handle(new GetEventRequest(id));

            if (response is null)
                return NotFound();

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve event {EventId}", id);
            return Problem(
                title: "An error occurred while retrieving the event.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
