using Microsoft.AspNetCore.Mvc;
using Booking.Features.EventCatalog;
using Booking.Features.Tickets;

namespace Booking.Features.Bookings;

[ApiController]
[Route("/[action]")]
public sealed class BookingsController(
    BookingService bookingService,
    ITicketServiceClient ticketServiceClient,
    ILogger<BookingsController> logger,
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
        [FromHeader(Name = "X-Guest-Access-Token")] string accessToken,
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

        var booking = await bookingService.GetBookingForGuestAsync(
            bookingId,
            accessToken,
            cancellationToken);
        if (booking is null)
            return NotFound();

        return Ok(ToDetailsResponse(booking));
    }

    [HttpGet("{bookingId:guid}/tickets")]
    [ProducesResponseType(typeof(IReadOnlyList<TicketServiceTicketResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTicketsForBooking(
        Guid bookingId,
        [FromHeader(Name = "X-Guest-Access-Token")] string accessToken,
        CancellationToken cancellationToken)
    {
        if (!await bookingService.GuestHasAccessAsync(bookingId, accessToken, cancellationToken))
            return NotFound();

        try
        {
            var tickets = await ticketServiceClient.GetTicketsForBookingAsync(bookingId, cancellationToken);
            return Ok(tickets.Where(ticket => ticket.BookingId == bookingId).ToArray());
        }
        catch (HttpRequestException exception)
        {
            return TicketServiceUnavailable(exception);
        }
    }

    [HttpGet("{bookingId:guid}/tickets/{ticketId:guid}/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadTicketPdf(
        Guid bookingId,
        Guid ticketId,
        [FromHeader(Name = "X-Guest-Access-Token")] string accessToken,
        [FromQuery] bool download,
        CancellationToken cancellationToken)
    {
        if (!await bookingService.GuestHasAccessAsync(bookingId, accessToken, cancellationToken))
            return NotFound();

        try
        {
            var pdf = await ticketServiceClient.GetTicketPdfAsync(
                bookingId, ticketId, download, cancellationToken);
            return download
                ? File(pdf, "application/pdf", $"ticket-{ticketId:N}.pdf")
                : File(pdf, "application/pdf");
        }
        catch (HttpRequestException exception)
        {
            if (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
                return NotFound();

            return TicketServiceUnavailable(exception);
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateBookingResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
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
                    "Tickets are temporarily reserved.",
                    result.GuestAccessToken))
            };
        }
        catch (BookingValidationException exception)
        {
            return RequestValidationProblem(exception);
        }
        catch (EventCatalogSnapshotNotFoundException)
        {
            return Conflict(new ProblemDetails
            {
                Title = "The event or section is no longer available.",
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (EventCatalogUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Event details are temporarily unavailable. Please try again.",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
    }

    [HttpPost("{bookingId:guid}")]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult ConfirmBooking(Guid bookingId) =>
        StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
            Title = "Direct confirmation is disabled. Use StartPayment to verify payment and confirm the booking.",
            Status = StatusCodes.Status403Forbidden
        });

    [HttpPost("{bookingId:guid}")]
    [ProducesResponseType(typeof(StartPaymentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(StartPaymentResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(StartPaymentResult), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(StartPaymentResult), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> StartPayment(
        Guid bookingId,
        [FromHeader(Name = "X-Guest-Access-Token")] string accessToken,
        CancellationToken cancellationToken)
    {
        if (!await bookingService.GuestHasAccessAsync(
            bookingId,
            accessToken,
            cancellationToken))
            return NotFound();

        try
        {
            var result = await bookingService.StartPaymentAsync(bookingId, cancellationToken);
            if (result.BookingStatus == "NotFound")
                return NotFound(result);
            if (result.PaymentStatus == "Invalid")
                return StatusCode(StatusCodes.Status502BadGateway, result);
            if (result.PaymentStatus is "Pending" or "Unknown")
                return Accepted(result);
            return result.BookingStatus == BookingStatuses.Confirmed ? Ok(result) : Conflict(result);
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
    public async Task<IActionResult> CancelBooking(
        Guid bookingId,
        [FromHeader(Name = "X-Guest-Access-Token")] string accessToken,
        CancellationToken cancellationToken)
    {
        if (!await bookingService.GuestHasAccessAsync(bookingId, accessToken, cancellationToken))
            return NotFound();

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

    private IActionResult TicketServiceUnavailable(HttpRequestException exception)
    {
        logger.LogError(exception, "TicketService request failed while serving a guest booking.");
        return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
        {
            Title = "Ticket service is temporarily unavailable.",
            Status = StatusCodes.Status502BadGateway
        });
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
    string Message,
    string GuestAccessToken);

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
