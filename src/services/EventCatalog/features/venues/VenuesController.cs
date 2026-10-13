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
using EventCatalog.Features.Venues.UpdateSection;

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
    UpdateSectionHandler updateSectionHandler,
    ILogger<VenuesController> logger) : ControllerBase
{
    [HttpPost]
    [Consumes("application/json")] //Ingen fil eller sökväg i projektet. Den betyder att innehållet är JSON.
    [ProducesResponseType(typeof(CreateVenueResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateVenue([FromBody] CreateVenueRequest request, CancellationToken cancellationToken)
    {
        // ApiController hanterar bindningsfel; handlern validerar requestens värden.
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
        catch (VenueValidationException ex)
        {
            return RequestValidationProblem(ex);
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
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetVenue([FromRoute] Guid id)
    {
        try
        {
            logger.LogInformation("Retrieving venue {VenueId}", id);

            var venue = await getVenueHandler.Handle(new GetVenueRequest(id));

            // Returnera 404 om lokalen saknas, annars 200 med lokalens uppgifter.
            if (venue is null)
                return NotFound();

            return Ok(venue);
        }
        catch (VenueValidationException ex)
        {
            return RequestValidationProblem(ex);
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
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateVenue([FromRoute] Guid id, [FromBody] UpdateVenueRequest request, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Updating venue {VenueId}", id);

            // Använd id från URL:en även om requesten innehåller ett annat VenueId.
            var updated = await updateVenueHandler.Handle(request with { VenueId = id }, cancellationToken);

            // Handlern returnerar false när lokalen saknas, vilket ger 404.
            if (!updated)
            {
                logger.LogWarning("Venue {VenueId} not found for update.", id);
                return NotFound();
            }

            // En lyckad uppdatering ger 204 utan svarskropp.
            return NoContent();
        }
        catch (VenueValidationException ex)
        {
            return RequestValidationProblem(ex);
        }
        catch (VenueCapacityConflictException ex)
        {
            return Problem(
                title: "The capacity conflicts with the existing venue configuration.",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteVenue([FromRoute] Guid id)
    {
        try
        {
            logger.LogInformation("Deleting venue {VenueId}", id);

            var request = new DeleteVenueRequest(id);
            var result = await deleteVenueHandler.Handle(request);

            if (result == DeleteVenueResult.NotFound)
                return NotFound();

            if (result == DeleteVenueResult.InUse)
            {
                return Problem(
                    title: "The venue cannot be deleted.",
                    detail: "The venue is used by events and cannot be deleted.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            // Returnera 204 först när lokalen har tagits bort.
            return NoContent();
        }
        catch (VenueValidationException ex)
        {
            return RequestValidationProblem(ex);
        }
        catch (Exception ex)
        {
            // Oväntade fel loggas och returneras som 500.
            logger.LogError(ex, "Failed to delete venue {VenueId}", id);
            return Problem(
                title: "An error occurred while deleting the venue.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("{venueId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(CreateSectionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSection(
        [FromRoute] Guid venueId,
        [FromBody] CreateSectionRequest request,
        CancellationToken cancellationToken)
    {
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
        catch (VenueValidationException ex)
        {
            return RequestValidationProblem(ex);
        }
        catch (VenueCapacityConflictException ex)
        {
            return Problem(
                title: "The capacity conflicts with the existing venue configuration.",
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create section for venue {VenueId}", venueId);
            return Problem(
                title: "An error occurred while creating the section.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpDelete("{venueId:guid}/{sectionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteSection(
        [FromRoute] Guid venueId,
        [FromRoute] Guid sectionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await deleteSectionHandler.Handle(
                new DeleteSectionRequest(venueId, sectionId),
                cancellationToken);

            if (result == DeleteSectionResult.InvalidVenueId)
            {
                ModelState.AddModelError(nameof(DeleteSectionRequest.VenueId), "VenueId must not be empty.");
                return ValidationProblem(ModelState);
            }

            if (result == DeleteSectionResult.InvalidSectionId)
            {
                ModelState.AddModelError(nameof(DeleteSectionRequest.SectionId), "SectionId must not be empty.");
                return ValidationProblem(ModelState);
            }

            if (result == DeleteSectionResult.NotFound)
                return NotFound();

            if (result == DeleteSectionResult.InUse)
            {
                return Problem(
                    title: "The section cannot be deleted.",
                    detail: "The section is used by an event. Remove its event connections first.",
                    statusCode: StatusCodes.Status409Conflict);
            }

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
    [HttpPut("{venueId:guid}/{sectionId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateSection(
        [FromRoute] Guid venueId,
        [FromRoute] Guid sectionId,
        [FromBody] UpdateSectionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await updateSectionHandler.Handle(venueId, sectionId, request, cancellationToken);
            if (result == UpdateSectionResult.NotFound)
                return NotFound();

            var conflict = result switch
            {
                UpdateSectionResult.InUse => "Capacity and seating type cannot be changed while the section is used by an event.",
                UpdateSectionResult.CapacityExceeded => "The combined section capacity cannot exceed the venue capacity.",
                UpdateSectionResult.SeatConflict => "Capacity cannot be lower than the number of seats, and seating type cannot change while seats exist.",
                _ => null
            };

            if (conflict is not null)
                return Problem(title: "The section cannot be updated.", detail: conflict,
                    statusCode: StatusCodes.Status409Conflict);

            return NoContent();
        }
        catch (VenueValidationException ex)
        {
            return RequestValidationProblem(ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update section {SectionId} for venue {VenueId}", sectionId, venueId);
            return Problem(title: "An error occurred while updating the section.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private IActionResult RequestValidationProblem(VenueValidationException exception)
    {
        foreach (var (field, messages) in exception.Errors)
        {
            foreach (var message in messages)
                ModelState.AddModelError(field, message);
        }

        return ValidationProblem(ModelState);
    }

}
