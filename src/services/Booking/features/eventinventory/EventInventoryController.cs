using Booking.Features.EventInventory.CreateEventInventory;
using Booking.Features.EventInventory.GetEventInventory;
using Booking.Features.EventInventory.ListEventInventory;
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
        if (eventId == Guid.Empty)
            return BadRequest("EventId must be a non-empty UUID.");

        return Ok(await listHandler.Handle(eventId, cancellationToken));
    }

    [HttpGet("inventory/{inventoryId:guid}")]
    public async Task<IActionResult> Get(Guid inventoryId, CancellationToken cancellationToken)
    {
        if (inventoryId == Guid.Empty)
            return BadRequest("InventoryId must be a non-empty UUID.");

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
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid inventory data.",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
}
