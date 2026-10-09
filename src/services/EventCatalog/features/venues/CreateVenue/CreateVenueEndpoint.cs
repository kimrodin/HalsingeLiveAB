using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using EventCatalog.Features.Venues.GetVenue;

namespace EventCatalog.Features.Venues.CreateVenue;

[Controller]
[ApiController]
[Route("venues")]
public class CreateVenueEndpoint : ControllerBase
{
    private readonly ILogger<CreateVenueEndpoint> logger;
    private readonly CreateVenueHandler handler;

    public CreateVenueEndpoint(ILogger<CreateVenueEndpoint> logger, CreateVenueHandler handler)
    {
        this.logger = logger;
        this.handler = handler;
    }

    // POST /venues tar emot en lokal som JSON och skapar den via handlern.
    [HttpPost]
    [Consumes("application/json")] //Ingen fil eller sökväg i projektet. Den betyder att innehållet är JSON.
    [ProducesResponseType(typeof(CreateVenueResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateVenue([FromBody] CreateVenueRequest request, CancellationToken cancellationToken)
    {
        // ApiController returnerar automatiskt 400 med fältfel för ogiltig request.
        try
        {
            logger.LogInformation("Creating venue {VenueName}", request.Name);

            var response = await handler.Handle(request, cancellationToken);

            // Returnerar 201 med den skapade lokalen och en Location-länk till GET-routen.
            return CreatedAtAction(
                nameof(GetVenueEndpoint.GetVenue),
                nameof(GetVenueEndpoint),
                new { id = response.VenueId },
                response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Låt ett avbrutet anrop fortsätta som avbrott i stället för ett 500-fel.
            throw;
        }
        catch (Exception ex)
        {
            // Logga det tekniska felet och returnera ett generellt felmeddelande till klienten.
            logger.LogError(ex, "Failed to create venue {VenueName}", request.Name);
            return Problem(
                title: "An error occurred while creating the venue.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
