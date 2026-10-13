using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using EventCatalog.Features.Venues.CreateVenue;
using EventCatalog.Features.Venues.GetVenue;
using EventCatalog.Features.Venues.ListVenues;
using EventCatalog.Features.Venues.UpdateVenue;
using EventCatalog.Features.Venues.DeleteVenue;
using EventCatalog.Features.Venues.CreateSection;
using EventCatalog.Features.Venues.DeleteSection;

namespace EventCatalog.Features.Venues;

[ApiController]
[Route("/[action]")]
public class VenuesController(
    CreateVenueHandler createVenueHandler,
    GetVenueHandler getVenueHandler,
    ListVenuesHandler listVenuesHandler,
    UpdateVenueHandler updateVenueHandler,
    DeleteVenueHandler deleteVenueHandler,
    CreateSectionHandler createSectionHandler,
    DeleteSectionHandler deleteSectionHandler,
    ILogger<VenuesController> logger) : ControllerBase
{
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

            var response = await createVenueHandler.Handle(request, cancellationToken);

            // Returnerar 201 med den skapade lokalen och en Location-länk till GET-routen.
            return CreatedAtAction(
                nameof(GetVenue),
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

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetVenueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetVenue([FromRoute] Guid id)
    {
        // Ett GUID med enbart nollor är giltigt i routen men avvisas med 400 här.
        if (id == Guid.Empty)
            return BadRequest("VenueId must not be empty.");

        try
        {
            logger.LogInformation("Retrieving venue {VenueId}", id);

            var venue = await getVenueHandler.Handle(new GetVenueRequest(id));

            // Returnera 404 om lokalen saknas, annars 200 med lokalens uppgifter.
            if (venue is null)
                return NotFound();

            return Ok(venue);
        }
        catch (Exception ex)
        {
            // Logga felet och returnera 500 med ProblemDetails.
            logger.LogError(ex, "Failed to retrieve venue {VenueId}", id);
            return Problem(
                title: "An error occurred while retrieving the venue.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet()]
    [ProducesResponseType(typeof(ListVenuesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]

    public async Task<IActionResult> ListVenues()
    {
        try
        {
            logger.LogInformation("Listing venues.");

            var venues = await listVenuesHandler.Handle();

            // Returnera 200 med svaret även när listan är tom.
            return Ok(venues);
        }
        catch (Exception ex)
        {
            // Logga felet och returnera 500 med ProblemDetails.
            logger.LogError(ex, "Failed to list venues.");
            return Problem(
                title: "An error occurred while listing the venues.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

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
            var updated = await updateVenueHandler.Handle(request with { VenueId = id });

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

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteVenue([FromRoute] Guid id)
    {
        // Avvisa ett tomt lokal-id innan handlern anropas.
        if (id == Guid.Empty)
            return BadRequest("VenueId must not be empty.");

        try
        {
            logger.LogInformation("Deleting venue {VenueId}", id);

            var request = new DeleteVenueRequest(id);
            await deleteVenueHandler.Handle(request);

            // När handlern har slutförts returneras 204 utan svarskropp.
            return NoContent();
        }
        catch (Exception ex)
        {
            // Nuvarande felhantering loggar alla undantag och returnerar 500.
            logger.LogError(ex, "Failed to delete venue {VenueId}", id);
            return Problem(
                title: "An error occurred while deleting the venue.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("{venueId:guid}/sections")]
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
            var response = await createSectionHandler.Handle(venueId, request, cancellationToken);
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

    [HttpDelete("{venueId:guid}/sections/{sectionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteSection(
        [FromRoute] Guid venueId,
        [FromRoute] Guid sectionId,
        CancellationToken cancellationToken)
    {
        if (venueId == Guid.Empty)
            return BadRequest("VenueId must not be empty.");

        if (sectionId == Guid.Empty)
            return BadRequest("SectionId must not be empty.");

        try
        {
            var deleted = await deleteSectionHandler.Handle(
                new DeleteSectionRequest(venueId, sectionId),
                cancellationToken);

            if (!deleted)
                return NotFound();

            return NoContent();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to delete section {SectionId} from venue {VenueId}",
                sectionId,
                venueId);

            return Problem(
                title: "An error occurred while deleting the section.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
