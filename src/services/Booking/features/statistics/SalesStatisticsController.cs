using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booking.Features.Statistics;

[ApiController]
[Route("/[action]")]
public sealed class SalesStatisticsController(SalesStatistics statistics, TimeProvider clock,
    ILogger<SalesStatisticsController> logger) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(SalesStatisticsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetSalesStatistics([FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        SalesPeriod period;
        try { period = SalesPeriod.Create(from, to, clock.GetUtcNow()); }
        catch (ArgumentException ex) { return BadRequest(new ProblemDetails { Title = ex.Message, Status = 400 }); }
        try { return Ok(await statistics.Get(period, cancellationToken)); }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException
            || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not produce sales statistics for {From}–{To}", period.From, period.To);
            return StatusCode(502, new ProblemDetails { Title = "Payment statistics are temporarily unavailable or inconsistent.", Status = 502 });
        }
    }
}
