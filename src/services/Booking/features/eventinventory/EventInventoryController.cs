using Booking.Features.EventInventory.CreateEventInventory;
using Booking.Features.EventInventory.GetEventInventory;
using Booking.Features.EventInventory.ListEventInventory;
using Booking.Features.Bookings;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Features.EventInventory;

[ApiController]
[Route("api")]
public class EventInventoryController(
    CreateEventInventoryHandler createHandler,
    GetEventInventoryHandler getHandler,
    ListEventInventoryHandler listHandler) : ControllerBase
{
    [HttpGet("events/{eventId:guid}/inventory")]
    public async Task<IActionResult> ListForEvent(Guid eventId, CancellationToken cancellationToken)
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

    [HttpGet("inventory/{inventoryId:guid}")]
    public async Task<IActionResult> Get(Guid inventoryId, CancellationToken cancellationToken)
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

    [HttpPost("events/{eventId:guid}/inventory")]
    public async Task<IActionResult> Create(
        Guid eventId,
        [FromBody] CreateEventInventoryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await createHandler.Handle(eventId, request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { inventoryId = created.InventoryId }, created);
        }
        catch (BookingValidationException exception)
        {
            return RequestValidationProblem(exception);
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
                Title = "Inventory already exists for this event and section.",
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
