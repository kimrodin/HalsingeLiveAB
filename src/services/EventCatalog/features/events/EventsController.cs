using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using EventCatalog.Features.Events;
using EventCatalog.Features.Events.CreateEvent;
using EventCatalog.Features.Events.GetEvent;
using EventCatalog.Features.Events.ListEvents;
using EventCatalog.Features.Events.UpdateEvent;
using EventCatalog.Features.Events.DeleteEvent;

namespace EventCatalog.Features.Events;

[ApiController]
[Route("/[action]")]
public class EventsController(
    CreateEventHandler createEventHandler,
    GetEventHandler getEventHandler,
    ListEventsHandler listEventsHandler,
    UpdateEventHandler updateEventHandler,
    DeleteEventHandler deleteEventHandler,
    ILogger<EventsController> logger) : ControllerBase
{
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(CreateEventResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ValidationProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    // TODO: Lägg till [Authorize(Policy = "EventAdmin")] när policyn finns i API-hosten.
    // TODO: Dokumentera 401 och 403 när behörighetskontrollen kopplas in.
    public async Task<IActionResult> CreateEvent(
        [FromBody] CreateEventRequest request,
        CancellationToken cancellationToken)
    {
        // [ApiController] hanterar bindningsfel och ogiltigt ModelState med 400.
        // Verksamhetsvalidering, exempelvis positiv kapacitet, hör till handlern.
        // ASP.NET Core binder JSON till requesten och injicerar handlern i konstruktorn.
        // Token skickas vidare så att handlerns databasarbete kan avbrytas.
        CreateEventResponse response;
        try
        {
            response = await createEventHandler.Handle(request, cancellationToken);
        }
        catch (EventValidationException ex)
        {
            return RequestValidationProblem(ex);
        }

        if (response is null)
            return NotFound("The venue or one of its sections was not found.");

        // TODO: När handlern returnerar valideringsfel, mappa dem till
        // ValidationProblem med fel per fält, exempelvis via ModelState.AddModelError.
        // Håll verksamhetsreglerna i handlern så att endpointen bara hanterar HTTP.
        // Oväntade fel bör hanteras centralt som 500, inte fångas här som 400.

        // TODO: Ange Location med CreatedAtAction(nameof(GetEvent), ...) när response innehåller EventId.
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEvent([FromRoute] Guid id)
    {
        try
        {
            logger.LogInformation("Retrieving event {EventId}", id);

            var response = await getEventHandler.Handle(new GetEventRequest(id));

            if (response is null)
                return NotFound();

            return Ok(response);
        }
        catch (EventValidationException ex)
        {
            return RequestValidationProblem(ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve event {EventId}", id);
            return Problem(
                title: "An error occurred while retrieving the event.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet()]
    [ProducesResponseType(typeof(ListEventsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]

    public async Task<IActionResult> ListEvents()
    {
        try
        {
            logger.LogInformation("Listing events.");

            var events = await listEventsHandler.Handle();

            return Ok(events);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list events.");
            return Problem(
                title: "An error occurred while listing the events.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateEvent([FromRoute] Guid id, [FromBody] UpdateEventRequest request, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Updating event {EventId}", id);

            var result = await updateEventHandler.Handle(id, request, cancellationToken);

            if (result == UpdateEventResult.EventNotFound)
            {
                logger.LogWarning("Event {EventId} not found for update.", id);
                return NotFound();
            }

            return NoContent();
        }
        catch (EventValidationException ex)
        {
            return RequestValidationProblem(ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update event {EventId}", id);
            return Problem(
                title: "An error occurred while updating the event.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    // TODO: Kräv administratörsbehörighet enligt projektets behörighetsmodell.

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteEvent([FromRoute] Guid id)
    {
        try
        {
            logger.LogInformation("Deleting event {EventId}", id);

            var request = new DeleteEventRequest(id);
            await deleteEventHandler.Handle(request);

            // När handlern har slutförts returneras 204 utan svarskropp.
            return NoContent();
        }
        catch (EventValidationException ex)
        {
            return RequestValidationProblem(ex);
        }
        catch (Exception ex)
        {
            // Nuvarande felhantering loggar alla undantag och returnerar 500.
            logger.LogError(ex, "Failed to delete event {EventId}", id);
            return Problem(
                title: "An error occurred while deleting the event.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private IActionResult RequestValidationProblem(EventValidationException exception)
    {
        foreach (var (field, messages) in exception.Errors)
        {
            foreach (var message in messages)
                ModelState.AddModelError(field, message);
        }

        return ValidationProblem(ModelState);
    }
}
