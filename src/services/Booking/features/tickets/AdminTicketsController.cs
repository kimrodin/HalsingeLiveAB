using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Features.Tickets;

[ApiController]
[Route("admin/tickets")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminTicketsController(
    ITicketServiceClient ticketServiceClient,
    ILogger<AdminTicketsController> logger) : ControllerBase
{
    [HttpPost("{ticketId:guid}/check-in")]
    [ProducesResponseType(typeof(TicketServiceStatusChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> CheckInTicket(
        Guid ticketId,
        CancellationToken cancellationToken,
        [FromQuery] Guid eventId = default)
    {
        if (eventId == Guid.Empty)
            return BadRequest(new ProblemDetails
            {
                Title = "A valid event ID is required for check-in.",
                Status = StatusCodes.Status400BadRequest
            });

        return await ChangeTicketStatusAsync(
            ticketId,
            (id, token) => ticketServiceClient.CheckInTicketAsync(id, eventId, token),
            "check-in",
            cancellationToken);
    }

    [HttpPost("{ticketId:guid}/cancel")]
    [ProducesResponseType(typeof(TicketServiceStatusChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public Task<IActionResult> CancelTicket(
        Guid ticketId,
        CancellationToken cancellationToken) =>
        ChangeTicketStatusAsync(
            ticketId,
            ticketServiceClient.CancelTicketAsync,
            "cancellation",
            cancellationToken);

    private async Task<IActionResult> ChangeTicketStatusAsync(
        Guid ticketId,
        Func<Guid, CancellationToken, Task<TicketServiceStatusChangeResponse>> changeStatus,
        string operation,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await changeStatus(ticketId, cancellationToken));
        }
        catch (HttpRequestException exception) when (
            exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound();
        }
        catch (HttpRequestException exception) when (
            exception.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            return Conflict(new ProblemDetails
            {
                Title = "The ticket is not valid for this operation.",
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "TicketService timed out while attempting to {Operation} ticket {TicketId}.",
                operation, ticketId);
            return StatusCode(StatusCodes.Status504GatewayTimeout, new ProblemDetails
            {
                Title = "Ticket service timed out. Please check the ticket status before retrying.",
                Status = StatusCodes.Status504GatewayTimeout
            });
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "TicketService failed to {Operation} ticket {TicketId}.", operation, ticketId);
            return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Title = "Ticket service is temporarily unavailable.",
                Status = StatusCodes.Status502BadGateway
            });
        }
    }
}
