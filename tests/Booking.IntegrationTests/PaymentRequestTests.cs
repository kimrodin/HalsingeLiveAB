using Booking.Features.Bookings;
using Booking.Features.Payments.Gateway;
using Microsoft.EntityFrameworkCore;

namespace Booking.IntegrationTests;

public sealed class PaymentRequestTests(BookingDatabaseFixture fixture) : IClassFixture<BookingDatabaseFixture>
{
    [BookingIntegrationFact]
    public async Task Start_persists_database_price_and_request_without_contacting_payment_service()
    {
        var gateway = new OfflineGateway();
        var inventory = await fixture.CreateInventoryAsync(price: 149.75m);
        var id = await fixture.ReserveAsync(inventory, gateway, 2);
        await using var db = fixture.CreateContext();
        var result = await fixture.CreateService(db, gateway).StartPaymentAsync(id);
        Assert.Equal("Pending", result.PaymentStatus);
        Assert.Equal(BookingStatuses.Reserved, result.BookingStatus);
        var row = await db.PaymentRequestOutboxMessages.SingleAsync(row => row.BookingId == id);
        Assert.Equal(299.50m, row.Amount);
        Assert.Equal("SEK", row.Currency);
        Assert.Null(row.DeliveredAtUtc);
        Assert.True((await db.Bookings.SingleAsync(row => row.BookingId == id)).PaymentRequested);
        Assert.Equal(0, gateway.LookupCalls);
    }

    [BookingIntegrationFact]
    public async Task Failed_outbox_insert_rolls_back_payment_intent_and_can_be_retried()
    {
        var gateway = new OfflineGateway();
        var inventory = await fixture.CreateInventoryAsync();
        var id = await fixture.ReserveAsync(inventory, gateway);
        await using (var db = fixture.CreateContext())
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"PaymentRequestOutboxMessages\" ADD CONSTRAINT test_insert_failure CHECK (FALSE) NOT VALID");
            try
            {
                await Assert.ThrowsAsync<DbUpdateException>(() => fixture.CreateService(db, gateway).StartPaymentAsync(id));
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"PaymentRequestOutboxMessages\" DROP CONSTRAINT test_insert_failure");
            }
        }
        await using var verify = fixture.CreateContext();
        Assert.False((await verify.Bookings.AsNoTracking().SingleAsync(row => row.BookingId == id)).PaymentRequested);
        Assert.False(await verify.PaymentRequestOutboxMessages.AnyAsync(row => row.BookingId == id));
        Assert.Equal("Pending", (await fixture.CreateService(verify, gateway).StartPaymentAsync(id)).PaymentStatus);
        Assert.Single(await verify.PaymentRequestOutboxMessages.Where(row => row.BookingId == id).ToListAsync());
    }

    [BookingIntegrationFact]
    public async Task Reconciliation_does_not_start_payments_or_create_requests_for_unrequested_bookings()
    {
        var gateway = new OfflineGateway { LookupUnavailable = false };
        var inventory = await fixture.CreateInventoryAsync();
        var id = await fixture.ReserveAsync(inventory, gateway);
        await using var db = fixture.CreateContext();
        var service = fixture.CreateService(db, gateway);
        Assert.Equal("NotStarted", (await service.ReconcilePaymentAsync(id)).PaymentStatus);
        Assert.Empty(await db.PaymentRequestOutboxMessages.Where(row => row.BookingId == id).ToListAsync());
        await service.StartPaymentAsync(id);
        var original = await db.PaymentRequestOutboxMessages.AsNoTracking().SingleAsync(row => row.BookingId == id);
        Assert.Equal("Pending", (await service.ReconcilePaymentAsync(id)).PaymentStatus);
        Assert.Equal("Pending", (await service.StartPaymentAsync(id)).PaymentStatus);
        Assert.Equal(original.MessageId, (await db.PaymentRequestOutboxMessages.AsNoTracking().SingleAsync(row => row.BookingId == id)).MessageId);
        Assert.Equal(2, gateway.LookupCalls);
    }

    [BookingIntegrationFact]
    public async Task Expired_queued_request_keeps_compensation_open_until_cancellation_is_verified()
    {
        var gateway = new OfflineGateway();
        var inventory = await fixture.CreateInventoryAsync();
        var id = await fixture.ReserveAsync(inventory, gateway);
        await using (var db = fixture.CreateContext())
        {
            await fixture.CreateService(db, gateway).StartPaymentAsync(id);
            await db.Bookings.Where(row => row.BookingId == id).ExecuteUpdateAsync(update =>
                update.SetProperty(row => row.ExpiresAt, DateTime.UtcNow.AddSeconds(-1)));
            Assert.Equal("Unknown", (await fixture.CreateService(db, gateway).ReconcilePaymentAsync(id)).PaymentStatus);
            Assert.False((await db.Bookings.AsNoTracking().SingleAsync(row => row.BookingId == id)).PaymentSettled);
        }
        gateway.LookupUnavailable = false;
        await using var recovery = fixture.CreateContext();
        var result = await fixture.CreateService(recovery, gateway).ReconcilePaymentAsync(id);
        Assert.Equal("Refunded", result.PaymentStatus);
        Assert.Equal(1, gateway.CancelCalls);
        Assert.True((await recovery.Bookings.AsNoTracking().SingleAsync(row => row.BookingId == id)).PaymentSettled);
        Assert.False(await recovery.TicketIssueOutboxMessages.AnyAsync(row => row.BookingId == id));
    }

    private sealed class OfflineGateway : IPaymentGateway
    {
        public bool LookupUnavailable { get; set; } = true;
        public int LookupCalls { get; private set; }
        public int CancelCalls { get; private set; }
        public Task<PaymentResult?> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken)
        {
            LookupCalls++;
            return LookupUnavailable ? Task.FromException<PaymentResult?>(new HttpRequestException("Payment service offline"))
                : Task.FromResult<PaymentResult?>(null);
        }
        public Task<PaymentResult> ProcessAsync(Guid bookingId, decimal amount, string currency, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Booking must never initiate payment over HTTP.");
        public Task<PaymentResult> RefundAsync(Guid paymentId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No charge exists.");
        public Task<PaymentResult> CancelAsync(Guid bookingId, decimal amount, string currency, CancellationToken cancellationToken)
        {
            CancelCalls++;
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new PaymentResult(Guid.NewGuid(), bookingId, amount, currency, "TimedOut", now, now, null, now));
        }
    }
}
