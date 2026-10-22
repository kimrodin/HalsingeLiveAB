using Microsoft.AspNetCore.Mvc;

namespace Booking.Features.Bookings;

[ApiController]
[Route("/[action]")]
public sealed class BookingsController(
    BookingService bookingService,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BookingDetailsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListBookings(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        var bookings = await bookingService.ListBookingsAsync(cancellationToken);
        return Ok(bookings.Select(ToDetailsResponse).ToArray());
    }

    [HttpGet("{bookingId:guid}")]
    [ProducesResponseType(typeof(BookingDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBooking(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        try
        {
            BookingValidation.ValidateId(bookingId, nameof(bookingId));
        }
        catch (BookingValidationException exception)
        {
            return RequestValidationProblem(exception);
        }

        var booking = await bookingService.GetBookingAsync(bookingId, cancellationToken);
        if (booking is null)
            return NotFound();

        return Ok(ToDetailsResponse(booking));
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateBookingResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateBooking(
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
                    result.BookingId!.Value,
                    result.ExpiresAt!.Value,
                    "Tickets are temporarily reserved."))
            };
        }
        catch (BookingValidationException exception)
        {
            return RequestValidationProblem(exception);
        }
    }

    [HttpPost("{bookingId:guid}")]
    [ProducesResponseType(typeof(BookingStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmBooking(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        BookingActionResult result;
        try
        {
            result = await bookingService.ConfirmAsync(bookingId, cancellationToken);
        }
        catch (BookingValidationException exception)
        {
            return RequestValidationProblem(exception);
        }
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

    [HttpPost("{bookingId:guid}")]
    [ProducesResponseType(typeof(BookingStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelBooking(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        BookingActionResult result;
        try
        {
            result = await bookingService.CancelAsync(bookingId, cancellationToken);
        }
        catch (BookingValidationException exception)
        {
            return RequestValidationProblem(exception);
        }
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

    private IActionResult RequestValidationProblem(BookingValidationException exception)
    {
        foreach (var (field, messages) in exception.Errors)
            foreach (var message in messages)
                ModelState.AddModelError(field, message);

        return ValidationProblem(ModelState);
    }

    private static BookingDetailsResponse ToDetailsResponse(
        Booking.Features.Bookings.Models.Booking booking) =>
        new(
            booking.BookingId,
            booking.CustomerName,
            booking.CustomerEmail,
            booking.Status,
            booking.TotalPrice,
            booking.CreatedAt,
            booking.ExpiresAt,
            booking.Items.Select(item => new BookingItemResponse(
                item.InventoryId,
                item.Quantity,
                item.UnitPrice)).ToArray());
}

public sealed record CreateBookingResponse(
    Guid BookingId,
    DateTimeOffset ExpiresAt,
    string Message);

public sealed record BookingDetailsResponse(
    Guid BookingId,
    string CustomerName,
    string CustomerEmail,
    string Status,
    decimal TotalPrice,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    IReadOnlyList<BookingItemResponse> Items);

public sealed record BookingItemResponse(Guid InventoryId, int Quantity, decimal UnitPrice);

public sealed record BookingStatusResponse(Guid BookingId, string Status);
