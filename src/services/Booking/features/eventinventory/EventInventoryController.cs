using Booking.Features.EventInventory.CreateEventInventory;
using Booking.Features.EventInventory.GetEventInventory;
using Booking.Features.EventInventory.ListEventInventory;
using Booking.Features.Bookings;
using Booking.Features.EventInventory.EventCatalog;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;

namespace Booking.Features.EventInventory;

[ApiController]
[Route("/[action]")]
public class EventInventoryController(
    CreateEventInventoryFromCatalogHandler createFromCatalogHandler,
    GetEventInventoryHandler getHandler,
    ListEventInventoryHandler listHandler,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAllEventInventory(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        return Ok(await listHandler.HandleAll(cancellationToken));
    }

    [HttpGet("{eventId:guid}")]
    public async Task<IActionResult> ListEventInventory(Guid eventId, CancellationToken cancellationToken)
    {
        try
        {
            BookingValidation.ValidateId(eventId, nameof(eventId));
        }
        catch (BookingValidationException exception)
        {
            return RequestValidationProblem(exception);
        }

        return Ok(await listHandler.Handle(eventId, cancellationToken));
    }

    [HttpGet("{inventoryId:guid}")]
    public async Task<IActionResult> GetEventInventory(Guid inventoryId, CancellationToken cancellationToken)
    {
        try
        {
            BookingValidation.ValidateId(inventoryId, nameof(inventoryId));
        }
        catch (BookingValidationException exception)
        {
            return RequestValidationProblem(exception);
        }

        var inventory = await getHandler.Handle(inventoryId, cancellationToken);
        return inventory is null ? NotFound() : Ok(inventory);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("from-catalog/{eventId:guid}")]
    public async Task<IActionResult> CreateEventInventoryFromCatalog(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        try
        {
            BookingValidation.ValidateId(eventId, nameof(eventId));
            var created = await createFromCatalogHandler.Handle(eventId, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (BookingValidationException exception)
        {
            return RequestValidationProblem(exception);
        }
        catch (EventCatalogNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Event not found in EventCatalog.",
                Status = StatusCodes.Status404NotFound
            });
        }
        catch (EventCatalogContractException exception)
        {
            return Problem(
                title: "EventCatalog returned invalid inventory data.",
                detail: exception.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (EventCatalogUnavailableException exception)
        {
            return Problem(
                title: "EventCatalog is unavailable.",
                detail: exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (InventoryConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Inventory already exists.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (DbUpdateException exception) when (IsDuplicateEventSection(exception))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Inventory already exists for one or more sections of this event.",
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    private static bool IsDuplicateEventSection(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_EventInventory_EventId_SectionId"
        };

    private IActionResult RequestValidationProblem(BookingValidationException exception)
    {
        foreach (var (field, messages) in exception.Errors)
            foreach (var message in messages)
                ModelState.AddModelError(field, message);

        return ValidationProblem(ModelState);
    }
}
