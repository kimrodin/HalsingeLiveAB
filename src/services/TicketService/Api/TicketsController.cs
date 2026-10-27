using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketService.Data;
using TicketService.Features.Tickets.DownloadTicket;
using TicketService.Features.Tickets.IssueTickets;

namespace TicketService.Api;

[ApiController]
[Route("tickets")]
public sealed class TicketsController(
    TicketDbContext dbContext,
    IssueTicketsHandler issueTicketsHandler,
    TicketDocumentService ticketDocumentService,
    IConfiguration configuration,
    ILogger<TicketsController> logger) : ControllerBase
{
    [HttpPost("issue-from-booking")]
    [ProducesResponseType(typeof(IssueTicketsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> IssueFromBooking(
        [FromBody] BookingConfirmedMessage message,
        [FromHeader(Name = "X-Internal-Api-Key")] string apiKey,
        CancellationToken cancellationToken)
    {
        var configuredApiKey = configuration["TicketService:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            logger.LogError("Ticket issuance is unavailable because TicketService:InternalApiKey is not configured.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Ticket issuance is not configured.",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }

        if (!IsValidApiKey(apiKey, configuredApiKey))
            return Unauthorized();

        try
        {
            var result = await issueTicketsHandler.HandleAsync(message, cancellationToken);
            return Ok(new IssueTicketsResponse(
                result.AlreadyIssued,
                result.Tickets.Select(ticket => new TicketResponse(
                    ticket.TicketId,
                    ticket.BookingId,
                    ticket.BookingItemId,
                    ticket.TicketNumberInBookingItem,
                    ticket.EventId,
                    ticket.EventName,
                    ticket.EventStartsAt,
                    ticket.VenueName,
                    ticket.SectionName,
                    ticket.UnitPrice,
                    ticket.Currency,
                    ticket.Status.ToString(),
                    ticket.IssuedAt)).ToArray()));
        }
        catch (TicketIssueValidationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "The booking confirmation is invalid.",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (TicketIssueConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "The booking item has already been processed with different data.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    [HttpGet("{ticketId:guid}")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTicket(Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.TicketId == ticketId, cancellationToken);

        if (ticket is null)
            return NotFound();

        return Ok(new TicketResponse(
            ticket.TicketId,
            ticket.BookingId,
            ticket.BookingItemId,
            ticket.TicketNumberInBookingItem,
            ticket.EventId,
            ticket.EventName,
            ticket.EventStartsAt,
            ticket.VenueName,
            ticket.SectionName,
            ticket.UnitPrice,
            ticket.Currency,
            ticket.Status.ToString(),
            ticket.IssuedAt));
    }

    [HttpGet("by-booking/{bookingId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<TicketResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTicketsByBooking(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var tickets = await dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.BookingId == bookingId)
            .OrderBy(ticket => ticket.BookingItemId)
            .ThenBy(ticket => ticket.TicketNumberInBookingItem)
            .ToArrayAsync(cancellationToken);

        return Ok(tickets.Select(ticket => new TicketResponse(
            ticket.TicketId,
            ticket.BookingId,
            ticket.BookingItemId,
            ticket.TicketNumberInBookingItem,
            ticket.EventId,
            ticket.EventName,
            ticket.EventStartsAt,
            ticket.VenueName,
            ticket.SectionName,
            ticket.UnitPrice,
            ticket.Currency,
            ticket.Status.ToString(),
            ticket.IssuedAt)).ToArray());
    }

    [HttpGet("{ticketId:guid}/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadTicketPdf(
        Guid ticketId,
        [FromQuery] bool download,
        CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.TicketId == ticketId, cancellationToken);

        if (ticket is null)
            return NotFound();

        var pdf = ticketDocumentService.GeneratePdf(ticket);
        return download
            ? File(pdf, "application/pdf", $"ticket-{ticket.TicketId:N}.pdf")
            : File(pdf, "application/pdf");
    }

    private static bool IsValidApiKey(string provided, string configured)
    {
        if (string.IsNullOrEmpty(provided))
            return false;

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        return CryptographicOperations.FixedTimeEquals(providedHash, configuredHash);
    }
}

public sealed record IssueTicketsResponse(bool AlreadyIssued, IReadOnlyList<TicketResponse> Tickets);

public sealed record TicketResponse(
    Guid TicketId,
    Guid BookingId,
    Guid BookingItemId,
    int TicketNumberInBookingItem,
    Guid EventId,
    string EventName,
    DateTimeOffset EventStartsAt,
    string VenueName,
    string SectionName,
    decimal UnitPrice,
    string Currency,
    string Status,
    DateTimeOffset IssuedAt);
