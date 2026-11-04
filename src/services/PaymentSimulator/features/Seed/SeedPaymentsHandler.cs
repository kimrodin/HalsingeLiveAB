using Microsoft.EntityFrameworkCore;
using PaymentSimulator.Data;
using PaymentSimulator.Features.Payments.Models;

namespace PaymentSimulator.Features.Seed;

public sealed class SeedPaymentsHandler(PaymentDbContext db)
{
    public async Task<int> Delete(CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(51000001)", ct);
        var count = await db.Payments.Where(row => row.BookingId.ToString().StartsWith("51000000-"))
            .ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        return count;
    }

    public async Task<int> Handle(IReadOnlyList<SeedPayment> rows, CancellationToken ct)
    {
        if (rows is null || rows.Count is < 1 or > 1000 || rows.Any(row => row is null)
            || rows.Select(row => row.BookingId).Distinct().Count() != rows.Count
            || rows.Any(row => !row.BookingId.ToString().StartsWith("51000000-", StringComparison.Ordinal)
                || row.Amount <= 0 || row.PaidAt.Offset != TimeSpan.Zero))
            throw new ArgumentException("Provide valid deterministic seed payments.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(51000001)", ct);
        var ids = rows.Select(row => row.BookingId).ToArray();
        var existing = await db.Payments.Where(p => ids.Contains(p.BookingId)).ToDictionaryAsync(p => p.BookingId, ct);
        var created = 0;
        foreach (var row in rows)
        {
            if (existing.TryGetValue(row.BookingId, out var payment))
            {
                if (payment.Amount != row.Amount || payment.PaidAt != row.PaidAt
                    || payment.Status != PaymentStatus.Succeeded || payment.Currency != "SEK" || payment.RefundedAt != null)
                    throw new InvalidOperationException("An existing seed payment has been changed.");
                continue;
            }
            db.Payments.Add(new Payment
            {
                Id = Guid.Parse(row.BookingId.ToString().Replace("51000000-", "54000000-")),
                BookingId = row.BookingId, Amount = row.Amount, Currency = "SEK",
                Status = PaymentStatus.Succeeded, PlannedOutcome = PaymentStatus.Succeeded,
                CreatedAt = row.PaidAt, CompletedAt = row.PaidAt, PaidAt = row.PaidAt, CompleteAfter = row.PaidAt,
            });
            created++;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return created;
    }
}

public sealed record SeedPayment(Guid BookingId, decimal Amount, DateTimeOffset PaidAt);
