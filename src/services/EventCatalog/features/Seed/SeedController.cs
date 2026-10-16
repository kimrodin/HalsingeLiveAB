using EventCatalog.Features.Seed.SeedEvents;
using EventCatalog.Features.Seed.SeedVenues;
using Microsoft.AspNetCore.Mvc;

namespace EventCatalog.Features.Seed;

[ApiController]
public sealed class SeedController(
    SeedVenuesHandler seedVenuesHandler,
    SeedEventsHandler seedEventsHandler,
    SeedAllHandler seedAllHandler,
    DeleteSeedDataHandler deleteSeedDataHandler,
    IWebHostEnvironment environment,
    ILogger<SeedController> logger) : ControllerBase
{
    [HttpPost("/venues/seed")]
    [ProducesResponseType(typeof(SeedVenuesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SeedVenues(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        try
        {
            return Ok(await seedVenuesHandler.Handle(cancellationToken));
        }
        catch (VenueSeedConflictException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "The venue seed data conflicts with existing records.",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed venues.");
            return Problem(title: "An error occurred while seeding venues.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("/events/seed")]
    [ProducesResponseType(typeof(SeedEventsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SeedEvents(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        try
        {
            return Ok(await seedEventsHandler.Handle(cancellationToken));
        }
        catch (EventSeedConflictException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "The event seed data conflicts with existing records.",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed events.");
            return Problem(title: "An error occurred while seeding events.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("/seed")]
    [ProducesResponseType(typeof(SeedAllResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SeedAll(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        try
        {
            return Ok(await seedAllHandler.Handle(cancellationToken));
        }
        catch (VenueSeedConflictException ex)
        {
            return SeedConflict("The venue seed data conflicts with existing records.", ex.Message);
        }
        catch (EventSeedConflictException ex)
        {
            return SeedConflict("The event seed data conflicts with existing records.", ex.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed venues and events.");
            return Problem(title: "An error occurred while seeding data.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpDelete("/seed")]
    [ProducesResponseType(typeof(DeleteSeedDataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteSeedData(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        try
        {
            return Ok(await deleteSeedDataHandler.Handle(cancellationToken));
        }
        catch (SeedDataDeleteConflictException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "The seed data cannot be deleted safely.",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete seed data.");
            return Problem(title: "An error occurred while deleting seed data.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static ObjectResult SeedConflict(string title, string detail) => new ObjectResult(new ProblemDetails
    {
        Title = title,
        Detail = detail,
        Status = StatusCodes.Status409Conflict
    })
    {
        StatusCode = StatusCodes.Status409Conflict
    };
}
