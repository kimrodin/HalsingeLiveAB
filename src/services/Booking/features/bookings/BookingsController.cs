using Microsoft.AspNetCore.Mvc;

namespace Booking.Features.Bookings;

[ApiController]
[Route("api/bookings")]
public sealed class BookingsController(BookingService bookingService) : ControllerBase
{
    [HttpGet("{bookingId:guid}")]
    [ProducesResponseType(typeof(BookingDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid bookingId, CancellationToken cancellationToken)
    {
        if (bookingId == Guid.Empty)
            return BadRequest("BookingId must be a non-empty UUID.");

        var booking = await bookingService.GetBookingAsync(bookingId, cancellationToken);
        if (booking is null)
            return NotFound();

        return Ok(new BookingDetailsResponse(
            booking.BookingId,
            booking.CustomerId,
            booking.Status,
            booking.TotalPrice,
            booking.CreatedAt,
            booking.ExpiresAt,
            booking.Items.Select(item => new BookingItemResponse(
                item.InventoryId,
                item.Quantity,
                item.UnitPrice)).ToArray()));
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateBookingResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await bookingService.CreateReservationAsync(request, cancellationToken);
            return result.Status switch
            {
                ReservationStatus.InventoryNotFound => NotFound(new ProblemDetails
                {
                    Title = "Inventory not found.",
                    Status = StatusCodes.Status404NotFound
                }),
                ReservationStatus.InsufficientInventory => Conflict(new ProblemDetails
                {
                    Title = "Not enough tickets are available.",
                    Status = StatusCodes.Status409Conflict
                }),
                _ => Accepted(new CreateBookingResponse(
                    result.ReservationId!.Value,
                    result.ExpiresAt!.Value,
                    "Tickets are temporarily reserved."))
            };
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid booking request.",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpPost("{bookingId:guid}/confirm")]
    [ProducesResponseType(typeof(BookingStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(Guid bookingId, CancellationToken cancellationToken)
    {
        var result = await bookingService.ConfirmAsync(bookingId, cancellationToken);
        return result.Status switch
        {
            BookingActionStatus.NotFound => NotFound(),
            BookingActionStatus.Completed => Ok(new BookingStatusResponse(bookingId, result.BookingStatus!)),
            BookingActionStatus.Expired => Conflict(new ProblemDetails
            {
                Title = "The reservation has expired.",
                Status = StatusCodes.Status409Conflict
            }),
            _ => Conflict(new ProblemDetails
            {
                Title = "The booking cannot be confirmed in its current state.",
                Detail = result.BookingStatus,
                Status = StatusCodes.Status409Conflict
            })
        };
    }

    [HttpPost("{bookingId:guid}/cancel")]
    [ProducesResponseType(typeof(BookingStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid bookingId, CancellationToken cancellationToken)
    {
        var result = await bookingService.CancelAsync(bookingId, cancellationToken);
        return result.Status switch
        {
            BookingActionStatus.NotFound => NotFound(),
            BookingActionStatus.Completed => Ok(new BookingStatusResponse(bookingId, result.BookingStatus!)),
            BookingActionStatus.Expired => Conflict(new ProblemDetails
            {
                Title = "The reservation has expired.",
                Status = StatusCodes.Status409Conflict
            }),
            _ => Conflict(new ProblemDetails
            {
                Title = "The booking cannot be cancelled in its current state.",
                Detail = result.BookingStatus,
                Status = StatusCodes.Status409Conflict
            })
        };
    }
}

public sealed record CreateBookingResponse(
    Guid ReservationId,
    DateTimeOffset ExpiresAt,
    string Message);

public sealed record BookingDetailsResponse(
    Guid BookingId,
    string CustomerId,
    string Status,
    decimal TotalPrice,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    IReadOnlyList<BookingItemResponse> Items);

public sealed record BookingItemResponse(Guid InventoryId, int Quantity, decimal UnitPrice);

public sealed record BookingStatusResponse(Guid BookingId, string Status);
