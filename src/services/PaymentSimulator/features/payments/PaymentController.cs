using Microsoft.AspNetCore.Mvc;
using PaymentSimulator.Features.Payments.GetPayment;
using PaymentSimulator.Features.Payments.ProcessPayment;

namespace PaymentSimulator.Features.Payments;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(
    GetPaymentHandler getPaymentHandler,
    ProcessPaymentHandler processPaymentHandler) : ControllerBase
{
    [HttpGet("{paymentId:guid}")]
    [ProducesResponseType(typeof(GetPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetPaymentResponse>> GetPayment(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        if (paymentId == Guid.Empty)
            return BadRequest(new ProblemDetails
            {
                Title = "Payment ID must be a non-empty UUID.",
                Status = StatusCodes.Status400BadRequest
            });

        var request = new GetPaymentRequest(paymentId);

        var response = await getPaymentHandler.Handle(
            request, cancellationToken);

        if (response is null)
            return NotFound();

        return Ok(response);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProcessPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProcessPaymentResponse>> ProcessPayment(
        [FromBody] ProcessPaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await processPaymentHandler.Handle(request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid payment request.",
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
}