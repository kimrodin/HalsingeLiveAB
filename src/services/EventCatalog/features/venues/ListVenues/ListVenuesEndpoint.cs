using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EventCatalog.Features.Venues.ListVenues;


[Controller]
[ApiController]
[Route("venues")]
public class ListVenuesEndpoint : ControllerBase
{

    private readonly ILogger<ListVenuesEndpoint> _logger;
    private readonly ListVenuesHandler _handler;

    public ListVenuesEndpoint(ILogger<ListVenuesEndpoint> logger, ListVenuesHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }

    // GET /venues hämtar listan över lokaler via handlern.
    [HttpGet()]
    [ProducesResponseType(typeof(ListVenuesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]

    public async Task<IActionResult> ListVenues()
    {
        try
        {
            _logger.LogInformation("Listing venues.");

            var venues = await _handler.Handle();

            // Returnera 200 med svaret även när listan är tom.
            return Ok(venues);
        }
        catch (Exception ex)
        {
            // Logga felet och returnera 500 med ProblemDetails.
            _logger.LogError(ex, "Failed to list venues.");
            return Problem(
                title: "An error occurred while listing the venues.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
