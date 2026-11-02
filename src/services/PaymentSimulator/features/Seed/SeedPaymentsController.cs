using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PaymentSimulator.Features.Seed;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/payments/seed")]
public sealed class SeedPaymentsController(SeedPaymentsHandler handler, IWebHostEnvironment environment) : ControllerBase
{
    [HttpDelete]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return NotFound();
        return Ok(new { DeletedPayments = await handler.Delete(ct) });
    }

    [HttpPost]
    public async Task<IActionResult> Seed([FromBody] SeedPayment[] rows, CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return NotFound();
        try { return Ok(new { CreatedCount = await handler.Handle(rows, ct) }); }
        catch (ArgumentException ex) { return BadRequest(new ProblemDetails { Title = ex.Message, Status = 400 }); }
        catch (InvalidOperationException ex) { return Conflict(new ProblemDetails { Title = ex.Message, Status = 409 }); }
    }
}
