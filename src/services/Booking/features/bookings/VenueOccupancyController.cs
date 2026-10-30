using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Features.Bookings;

[ApiController]
[Route("/[action]")]
public sealed class VenueOccupancyController(VenueOccupancyReport report) : ControllerBase
{
    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    public async Task<IActionResult> GetVenueOccupancy([FromQuery] DateTime? from,
        [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        if (from > to) return BadRequest("from must be on or before to.");
        try { return Ok(await report.Get(from, to, cancellationToken)); }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new ProblemDetails { Title = "Event catalog is temporarily unavailable.", Status = 502 });
        }
    }
}
