using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EventCatalog.Features.Events.ListEvents;


[Controller]
[ApiController]
[Route("events")]
public class ListEventsEndpoint : ControllerBase
{
    // TODO: Registrera GET /events och anropa handlern.
    // TODO: Returnera 200 med ListEventsResponse även när listan är tom.
    // TODO: Om sökning eller paginering införs: bind och validera query-parametrarna.
    // TODO: Dokumentera svar och eventuella query-parametrar i OpenAPI; skicka vidare CancellationToken.

    private readonly ILogger<ListEventsEndpoint> _logger;
    private readonly ListEventsHandler _handler;

    public ListEventsEndpoint(ILogger<ListEventsEndpoint> logger, ListEventsHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }

    [HttpGet()]
    [ProducesResponseType(typeof(ListEventsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]

    public async Task<IActionResult> ListEvents()
    {
        try
        {
            _logger.LogInformation("Listing events.");

            var events = await _handler.Handle();

            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list events.");
            return Problem(
                title: "An error occurred while listing the events.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}