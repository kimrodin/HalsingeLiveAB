using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EventCatalog.Features.Venues.ListVenues;


[Controller]
[ApiController]
[Route("venues")]
public class ListVenuesEndpoint : ControllerBase
{
    // TODO: Registrera GET /venues och anropa handlern.
    // TODO: Returnera 200 med ListVenuesResponse även när listan är tom.
    // TODO: Om sökning eller paginering införs: bind och validera query-parametrarna.
    // TODO: Dokumentera svar och eventuella query-parametrar i OpenAPI; skicka vidare CancellationToken.

    private readonly ILogger<ListVenuesEndpoint> _logger;
    private readonly ListVenuesHandler _handler;

    public ListVenuesEndpoint(ILogger<ListVenuesEndpoint> logger, ListVenuesHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }

    [HttpGet()]
    [ProducesResponseType(typeof(ListVenuesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]

    public async Task<IActionResult> ListVenues()
    {
        try
        {
            _logger.LogInformation("Listing venues.");

            var venues = await _handler.Handle();

            return Ok(venues);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list venues.");
            return Problem(
                title: "An error occurred while listing the venues.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
