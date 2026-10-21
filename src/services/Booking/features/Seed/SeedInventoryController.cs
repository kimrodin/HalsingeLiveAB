using Microsoft.AspNetCore.Mvc;

namespace Booking.Features.Seed;

[ApiController]
[Route("api/seed")]
public sealed class SeedInventoryController(
    SeedInventoryHandler handler,
    IWebHostEnvironment environment,
    ILogger<SeedInventoryController> logger) : ControllerBase
{
    [HttpPost("inventory")]
    [ProducesResponseType(typeof(SeedInventoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Seed(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        try
        {
            return Ok(await handler.Handle(cancellationToken));
        }
        catch (SeedInventoryConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "The mock inventory seed conflicts with existing inventory.",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to seed Booking inventory.");
            return Problem(
                title: "An error occurred while seeding Booking inventory.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
