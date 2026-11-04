using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Booking.Features.EventCatalog;

namespace Booking.Features.Seed.SeedPurchases;

[ApiController]
[Authorize(Policy = "AdminOnly")]
public sealed class SeedPurchasesController(SeedPurchasesHandler handler, DeleteSeedPurchasesHandler deleteHandler, IWebHostEnvironment environment,
    ILogger<SeedPurchasesController> logger) : ControllerBase
{
    [HttpDelete("/SeedPurchases")]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return NotFound();
        try { return Ok(await deleteHandler.Handle(ct)); }
        catch (SeedPurchasesConflictException ex) { return Conflict(new ProblemDetails { Title = ex.Message, Status = 409 }); }
        catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Could not finish seed deletion; run deletion again to resume.");
            return StatusCode(502, new ProblemDetails { Title = "Seed deletion could not reach a required service. Run deletion again to resume.", Status = 502 });
        }
    }

    [HttpPost("/SeedPurchases")]
    public async Task<IActionResult> Seed([FromBody] SeedPurchasesRequest request, CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return NotFound();
        try { return Ok(await handler.Handle(request.EventIds, ct)); }
        catch (ArgumentException ex) { return BadRequest(new ProblemDetails { Title = ex.Message, Status = 400 }); }
        catch (SeedPurchasesConflictException ex) { return Conflict(new ProblemDetails { Title = ex.Message, Status = 409 }); }
        catch (Exception ex) when (ex is HttpRequestException or EventCatalogUnavailableException or EventCatalogSnapshotNotFoundException
            || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Could not provision seed purchases; rerun the seed to resume.");
            return StatusCode(502, new ProblemDetails { Title = "Seed purchases could not reach a required service. Run the seed again to resume.", Status = 502 });
        }
    }
}

public sealed record SeedPurchasesRequest(Guid[] EventIds);
