using Microsoft.AspNetCore.Mvc;

namespace Booking.Features.Seed;

[ApiController]
[Route("/[action]")]
public sealed class SeedInventoryController(
    SeedInventoryHandler handler,
    DeleteSeedInventoryHandler deleteSeedInventoryHandler,
    ClearBookingDatabaseHandler clearBookingDatabaseHandler,
    IWebHostEnvironment environment,
    ILogger<SeedInventoryController> logger) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(SeedInventoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SeedInventory(CancellationToken cancellationToken)
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

    [HttpDelete]
    [ProducesResponseType(typeof(DeleteSeedInventoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteSeedInventory(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        try
        {
            return Ok(await deleteSeedInventoryHandler.Handle(cancellationToken));
        }
        catch (SeedInventoryDeleteConflictException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Seed inventory is in use.",
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
            logger.LogError(exception, "Failed to delete Booking seed inventory.");
            return Problem(
                title: "An error occurred while deleting Booking seed inventory.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpDelete]
    [ProducesResponseType(typeof(ClearBookingDatabaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ClearBookingDatabase(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        try
        {
            return Ok(await clearBookingDatabaseHandler.Handle(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to empty the Booking database.");
            return Problem(
                title: "An error occurred while emptying the Booking database.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
