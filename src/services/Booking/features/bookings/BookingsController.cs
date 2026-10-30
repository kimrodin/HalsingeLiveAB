using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using Booking.Features.EventCatalog;
using Booking.Features.Payments;
using Booking.Features.Tickets;

namespace Booking.Features.Bookings;

[ApiController]
[Route("/[action]")]
public sealed class BookingsController(
    BookingService bookingService,
    IPaymentGateway paymentGateway,
    ITicketServiceClient ticketServiceClient,
    ILogger<BookingsController> logger) : ControllerBase
{
    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    [ProducesResponseType(typeof(ListBookingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListBookings(
        [FromQuery] ListBookingsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await bookingService.ListBookingsAsync(request, cancellationToken);
        return Ok(new ListBookingsResponse(
            result.Bookings.Select(ToAdminListResponse).ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize,
            result.Statuses));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("{bookingId:guid}")]
    [ProducesResponseType(typeof(AdminBookingDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAdminBookingDetails(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var booking = await bookingService.GetBookingAsync(bookingId, cancellationToken);
        if (booking is null)
            return NotFound();

        PaymentResult payment;
        IReadOnlyList<TicketServiceTicketResponse> tickets;
        try
        {
            payment = await paymentGateway.GetByBookingAsync(bookingId, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or PaymentContractException)
        {
            logger.LogError(exception, "Payment lookup failed for admin booking {BookingId}.", bookingId);
            return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Title = "Payment details are temporarily unavailable.",
                Status = StatusCodes.Status502BadGateway
            });
        }

        try
        {
            tickets = await ticketServiceClient.GetTicketsForBookingAsync(bookingId, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            return TicketServiceUnavailable(exception);
        }

        var history = await bookingService.GetBookingHistoryAsync(
            booking.BookingId, booking.CustomerEmail, cancellationToken);
        return Ok(new AdminBookingDetailsResponse(
            ToAdminListResponse(booking),
            history.Select(item => new BookingHistoryItem(
                item.BookingId,
                item.Status,
                item.TotalPrice,
                item.CreatedAt)).ToArray(),
            payment,
            tickets));
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut("{bookingId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateBookingCustomer(
        Guid bookingId,
        [FromBody] UpdateBookingCustomerRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName))
            ModelState.AddModelError(nameof(request.CustomerName), "Kundnamn får inte vara tomt.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var updated = await bookingService.UpdateBookingCustomerAsync(
            bookingId, request.CustomerName, request.CustomerEmail, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("{bookingId:guid}/{ticketId:guid}")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAdminTicketPdf(
        Guid bookingId,
        Guid ticketId,
        [FromQuery] bool download,
        CancellationToken cancellationToken)
    {
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

    private static AdminBookingListItemResponse ToAdminListResponse(
        Booking.Features.Bookings.Models.Booking booking) =>
        new(
            booking.BookingId,
            booking.CustomerName,
            booking.CustomerEmail,
            booking.Status,
            booking.TotalPrice,
            booking.CreatedAt,
            booking.ExpiresAt,
            booking.CancelledAt,
            booking.PaymentRequested,
            booking.PaymentSettled,
            booking.Items.Select(item => new AdminBookingListItem(
                item.InventoryId,
                item.Quantity,
                item.UnitPrice,
                item.Currency,
                item.EventName,
                item.EventStartsAt,
                item.VenueName,
                item.SectionName)).ToArray());
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

public sealed record AdminBookingListItemResponse(
    Guid BookingId,
    string CustomerName,
    string CustomerEmail,
    string Status,
    decimal TotalPrice,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    DateTime? CancelledAt,
    bool PaymentRequested,
    bool PaymentSettled,
    IReadOnlyList<AdminBookingListItem> Items);

public sealed record AdminBookingDetailsResponse(
    AdminBookingListItemResponse Booking,
    IReadOnlyList<BookingHistoryItem> History,
    PaymentResult Payment,
    IReadOnlyList<TicketServiceTicketResponse> Tickets);

public sealed record BookingHistoryItem(Guid BookingId, string Status, decimal TotalPrice, DateTime CreatedAt);

public sealed record UpdateBookingCustomerRequest(
    [property: Required, StringLength(200)] string CustomerName,
    [property: Required, EmailAddress, StringLength(254)] string CustomerEmail);

public sealed record AdminBookingListItem(
    Guid InventoryId,
    int Quantity,
    decimal UnitPrice,
    string Currency,
    string EventName,
    DateTimeOffset EventStartsAt,
    string VenueName,
    string SectionName);

public sealed record ListBookingsRequest(
    string Search = "",
    string Status = "",
    DateTime? From = null,
    DateTime? To = null,
    string SortBy = "",
    string SortDirection = "",
    int Page = 1,
    int PageSize = 10);

public sealed record ListBookingsResponse(
    IReadOnlyList<AdminBookingListItemResponse> Bookings,
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyList<string> Statuses);

public sealed record BookingStatusResponse(Guid BookingId, string Status);
