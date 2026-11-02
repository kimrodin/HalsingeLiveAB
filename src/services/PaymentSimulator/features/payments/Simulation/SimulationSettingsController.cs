using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentSimulator.Features.Payments.ProcessPayment;

namespace PaymentSimulator.Features.Payments.Simulation;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/admin/payment-simulation")]
public sealed class SimulationSettingsController(
    SimulationSettingsService settingsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaymentSimulationSettings>> Get(
        CancellationToken cancellationToken)
    {
        return Ok(await settingsService.Get(cancellationToken));
    }

    [HttpPut]
    public async Task<ActionResult<PaymentSimulationSettings>> Update(
        [FromBody] PaymentSimulationSettings settings, CancellationToken cancellationToken)
    {
        try { return Ok(await settingsService.Update(settings, cancellationToken)); }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = ex.Message, Status = 400 });
        }
    }

}
