using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentSimulator.Data;
using PaymentSimulator.Features.Payments.GetPayment;

namespace PaymentSimulator.Features.Payments.Statistics;

[ApiController]
[Route("api/payments/sales")]
public sealed class PaymentSalesController(PaymentDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Get([FromQuery] PaymentSalesRequest request, CancellationToken cancellationToken)
    {
        if (request.From is null || request.ToExclusive is null
            || request.From >= request.ToExclusive
            || (request.ToExclusive.Value - request.From.Value).TotalDays > 734
            || request.PageSize is < 1 or > 500
            || request.AfterPaidAt.HasValue != request.AfterPaymentId.HasValue
            || request.AfterPaymentId == Guid.Empty
            || request.AfterPaidAt < request.From || request.AfterPaidAt >= request.ToExclusive)
            return BadRequest(new ProblemDetails { Title = "Invalid sales range, cursor or page size.", Status = 400 });

        var from = request.From.Value.ToUniversalTime();
        var to = request.ToExclusive.Value.ToUniversalTime();
        var query = db.Payments.AsNoTracking().Where(p => p.PaidAt >= from && p.PaidAt < to);
        if (request.AfterPaidAt.HasValue)
        {
            var after = request.AfterPaidAt.Value.ToUniversalTime();
            var id = request.AfterPaymentId!.Value;
            query = query.Where(p => p.PaidAt > after || p.PaidAt == after && p.Id.CompareTo(id) > 0);
        }
        // PaidAt is immutable after success. An ID tie-breaker avoids skipping rows
        // when multiple payments were completed at the same timestamp.
        var rows = await query.OrderBy(p => p.PaidAt).ThenBy(p => p.Id)
            .Take(request.PageSize + 1)
            .Select(p => new GetPaymentResponse(p.Id, p.BookingId, p.Amount, p.Currency, p.Status,
                p.CreatedAt, p.CompletedAt, p.PaidAt, p.RefundedAt))
            .ToArrayAsync(cancellationToken);
        var page = rows.Take(request.PageSize).ToArray();
        var next = rows.Length > request.PageSize
            ? new PaymentSalesCursor(page[^1].PaidAt!.Value, page[^1].PaymentId) : null;
        return Ok(new PaymentSalesPage(page, next));
    }
}

public sealed record PaymentSalesRequest(DateTimeOffset? From, DateTimeOffset? ToExclusive,
    DateTimeOffset? AfterPaidAt = null, Guid? AfterPaymentId = null, int PageSize = 500);
public sealed record PaymentSalesCursor(DateTimeOffset PaidAt, Guid PaymentId);
public sealed record PaymentSalesPage(GetPaymentResponse[] Payments, PaymentSalesCursor? Next);
