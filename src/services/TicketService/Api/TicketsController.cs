using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketService.Data;
using TicketService.Features.Tickets.DownloadTicket;
using TicketService.Features.Tickets.IssueTickets;
using TicketService.Features.Tickets.Models;

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
        var authorizationFailure = AuthorizeInternalRequest(apiKey);
        if (authorizationFailure is not null)
            return authorizationFailure;

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

    [HttpGet("by-booking/{bookingId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<TicketResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTicketsByBooking(
        Guid bookingId,
        [FromHeader(Name = "X-Internal-Api-Key")] string apiKey,
        CancellationToken cancellationToken)
    {
        var authorizationFailure = AuthorizeInternalRequest(apiKey);
        if (authorizationFailure is not null)
            return authorizationFailure;

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

    [HttpGet("by-booking/{bookingId:guid}/{ticketId:guid}/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadTicketPdf(
        Guid bookingId,
        Guid ticketId,
        [FromQuery] bool download,
        [FromHeader(Name = "X-Internal-Api-Key")] string apiKey,
        CancellationToken cancellationToken)
    {
        var authorizationFailure = AuthorizeInternalRequest(apiKey);
        if (authorizationFailure is not null)
            return authorizationFailure;

        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.TicketId == ticketId && item.BookingId == bookingId,
                cancellationToken);

        if (ticket is null)
            return NotFound();

        var pdf = ticketDocumentService.GeneratePdf(ticket);
        return download
            ? File(pdf, "application/pdf", $"ticket-{ticket.TicketId:N}.pdf")
            : File(pdf, "application/pdf");
    }

    [HttpPost("{ticketId:guid}/check-in")]
    [ProducesResponseType(typeof(TicketStatusChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> CheckInTicket(
        Guid ticketId,
        [FromHeader(Name = "X-Internal-Api-Key")] string apiKey,
        CancellationToken cancellationToken) =>
        ChangeTicketStatusAsync(ticketId, TicketStatus.CheckedIn, apiKey, cancellationToken);

    [HttpPost("{ticketId:guid}/cancel")]
    [ProducesResponseType(typeof(TicketStatusChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> CancelTicket(
        Guid ticketId,
        [FromHeader(Name = "X-Internal-Api-Key")] string apiKey,
        CancellationToken cancellationToken) =>
        ChangeTicketStatusAsync(ticketId, TicketStatus.Cancelled, apiKey, cancellationToken);

    private async Task<IActionResult> ChangeTicketStatusAsync(
        Guid ticketId,
        TicketStatus newStatus,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var authorizationFailure = AuthorizeInternalRequest(apiKey);
        if (authorizationFailure is not null)
            return authorizationFailure;

        if (ticketId == Guid.Empty)
            return BadRequest(new ProblemDetails
            {
                Title = "A ticket ID is required.",
                Status = StatusCodes.Status400BadRequest
            });

        var ticket = await dbContext.Tickets
            .SingleOrDefaultAsync(item => item.TicketId == ticketId, cancellationToken);
        if (ticket is null)
            return NotFound();

        if (ticket.Status != TicketStatus.Valid)
            return InvalidTicketState(ticket);

        var changedAt = DateTimeOffset.UtcNow;
        ticket.Status = newStatus;
        if (newStatus == TicketStatus.CheckedIn)
            ticket.CheckedInAt = changedAt;
        else
            ticket.CancelledAt = changedAt;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbContext.Entry(ticket).ReloadAsync(cancellationToken);
            return InvalidTicketState(ticket);
        }

        return Ok(new TicketStatusChangeResponse(ticket.TicketId, ticket.Status.ToString(), changedAt));
    }

    private static ConflictObjectResult InvalidTicketState(Ticket ticket) => new(new ProblemDetails
    {
        Title = "The ticket is not valid for this operation.",
        Detail = $"Current ticket status: {ticket.Status}.",
        Status = StatusCodes.Status409Conflict
    });

    private IActionResult AuthorizeInternalRequest(string apiKey)
    {
        var configuredApiKey = configuration["TicketService:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            logger.LogError("Ticket API is unavailable because TicketService:InternalApiKey is not configured.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Ticket API is not configured.",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }

        return IsValidApiKey(apiKey, configuredApiKey) ? null : Unauthorized();
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

public sealed record TicketStatusChangeResponse(
    Guid TicketId,
    string Status,
    DateTimeOffset ChangedAt);
