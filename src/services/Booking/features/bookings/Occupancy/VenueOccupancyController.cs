using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Features.Bookings.Occupancy;

[ApiController]
[Route("/[action]")]
public sealed class VenueOccupancyController(VenueOccupancyReport report) : ControllerBase
{
    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<VenueOccupancyRow>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetVenueOccupancy([FromQuery] DateTime? from,
        [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        if (from?.Date > to?.Date || from?.Year < 2 || to?.Year < 2
            || to?.Date == DateTime.MaxValue.Date)
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid date range.",
                Detail = "Choose calendar dates from year 2 through 9999-12-30, with from on or before to.",
                Status = StatusCodes.Status400BadRequest
            });
        try { return Ok(await report.Get(from, to, cancellationToken)); }
        catch (InvalidDataException)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new ProblemDetails { Title = "Event catalog returned incomplete event data.", Status = 502 });
        }
        catch (InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ProblemDetails { Title = "Inventory data is inconsistent.", Status = 500 });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new ProblemDetails { Title = "Event catalog is temporarily unavailable.", Status = 502 });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new ProblemDetails { Title = "Event catalog request timed out.", Status = 502 });
        }
    }
}
