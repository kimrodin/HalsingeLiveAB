using Booking.Data;
using Booking.Features.Bookings;
using Booking.Features.Payments;
using HalsingeLive.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Booking.IntegrationTests;

public sealed class PaymentResultTests(BookingDatabaseFixture fixture) : IClassFixture<BookingDatabaseFixture>
{
    [BookingIntegrationFact]
    public async Task Success_and_duplicate_events_confirm_once_and_share_HTTP_reconciliation_lock()
    {
        var gateway = new ResultGateway();
        var id = await CreateBookingAsync(gateway);
        var message = Success(gateway);
        await ReceiveAsync(message);
        await ReceiveAsync(message);
        await ReceiveAsync(message with { MessageId = Guid.NewGuid() });
        await using (var db = fixture.CreateContext())
        {
            await Task.WhenAll(DispatchAsync(gateway), fixture.CreateService(db, gateway).StartPaymentAsync(id));
        }
        await RetryNowAsync();
        await DispatchAsync(gateway);
        await using var verify = fixture.CreateContext();
        Assert.Equal(BookingStatuses.Confirmed, (await verify.Bookings.SingleAsync(row => row.BookingId == id)).Status);
        Assert.Equal(1, await verify.TicketIssueOutboxMessages.CountAsync(row => row.BookingId == id));
        Assert.Equal(2, await verify.PaymentResultInboxMessages.CountAsync(row => row.BookingId == id && row.ProcessedAtUtc != null));
        Assert.Equal(9, await verify.EventInventory.Where(row => row.BookingItems.Any(item => item.BookingId == id))
            .Select(row => row.AvailableQuantity).SingleAsync());
        Assert.Equal(0, gateway.RefundCalls);
    }

    [BookingIntegrationFact]
    public async Task Terminal_failures_release_reservations_without_issuing_tickets()
    {
        foreach (var outcome in new[] { "Failed", "TimedOut" })
        {
            var gateway = new ResultGateway();
            var id = await CreateBookingAsync(gateway, outcome);
            await using (var db = fixture.CreateContext())
                await new PaymentResultInbox(db, TimeProvider.System).ReceiveAsync(Failure(gateway));
            await DispatchAsync(gateway);
            await using var verify = fixture.CreateContext();
            var booking = await verify.Bookings.Include(row => row.Items).ThenInclude(item => item.EventInventory)
                .SingleAsync(row => row.BookingId == id);
            Assert.Equal(BookingStatuses.Cancelled, booking.Status);
            Assert.True(booking.PaymentSettled);
            Assert.False(await verify.TicketIssueOutboxMessages.AnyAsync(row => row.BookingId == id));
            var inventory = Assert.Single(booking.Items).EventInventory;
            Assert.Equal(10, await fixture.Reservations.GetAvailableQuantityAsync(inventory.EventId, inventory.SectionId, 10));
        }
    }

    [BookingIntegrationFact]
    public async Task Late_success_keeps_refund_work_durable_after_failure_and_handles_replay_after_refund()
    {
        var gateway = new ResultGateway { FailFirstRefund = true };
        var id = await CreateBookingAsync(gateway);
        var message = Success(gateway);
        await using (var db = fixture.CreateContext())
            await db.Bookings.Where(row => row.BookingId == id).ExecuteUpdateAsync(update =>
                update.SetProperty(row => row.ExpiresAt, DateTime.UtcNow.AddSeconds(-1)));
        await ReceiveAsync(message);
        await DispatchAsync(gateway);
        await using (var db = fixture.CreateContext())
        {
            var receipt = await db.PaymentResultInboxMessages.SingleAsync(row => row.MessageId == message.MessageId);
            Assert.Null(receipt.ProcessedAtUtc);
            Assert.Null(receipt.RejectedAtUtc);
            Assert.Equal(1, receipt.AttemptCount);
        }
        await RetryNowAsync();
        await DispatchAsync(gateway);
        Assert.NotNull(gateway.Current!.RefundedAt);
        await ReceiveAsync(message with { MessageId = Guid.NewGuid() });
        await DispatchAsync(gateway);
        Assert.Equal(2, gateway.RefundCalls);
        await using var verify = fixture.CreateContext();
        var booking = await verify.Bookings.SingleAsync(row => row.BookingId == id);
        Assert.Equal(BookingStatuses.Expired, booking.Status);
        Assert.True(booking.PaymentSettled);
        Assert.False(await verify.TicketIssueOutboxMessages.AnyAsync(row => row.BookingId == id));
        Assert.Equal(2, await verify.PaymentResultInboxMessages.CountAsync(row => row.BookingId == id && row.ProcessedAtUtc != null));
    }

    [BookingIntegrationFact]
    public async Task Historical_success_after_refund_does_not_confirm_or_issue_tickets()
    {
        var gateway = new ResultGateway();
        var id = await CreateBookingAsync(gateway);
        var message = Success(gateway);
        gateway.Current = gateway.Current! with { RefundedAt = DateTimeOffset.UtcNow };
        await ReceiveAsync(message);
        await DispatchAsync(gateway);
        await using var verify = fixture.CreateContext();
        Assert.Equal(BookingStatuses.Cancelled, (await verify.Bookings.SingleAsync(row => row.BookingId == id)).Status);
        Assert.False(await verify.TicketIssueOutboxMessages.AnyAsync(row => row.BookingId == id));
        Assert.Equal(0, gateway.RefundCalls);
    }

    [BookingIntegrationFact]
    public async Task Inbox_rejects_changed_payloads_and_quarantines_conflicting_results_without_regressing_booking()
    {
        var gateway = new ResultGateway();
        var id = await CreateBookingAsync(gateway);
        var message = Success(gateway);
        await ReceiveAsync(message);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReceiveAsync(message with { Amount = message.Amount + 1 }));
        await DispatchAsync(gateway);
        var failed = Failure(gateway) with { FailureCode = "Failed" };
        await using (var db = fixture.CreateContext())
            await new PaymentResultInbox(db, TimeProvider.System).ReceiveAsync(failed);
        await DispatchAsync(gateway);
        await using var verify = fixture.CreateContext();
        Assert.NotNull((await verify.PaymentResultInboxMessages.SingleAsync(row => row.MessageId == failed.MessageId)).RejectedAtUtc);
        Assert.Equal(BookingStatuses.Confirmed, (await verify.Bookings.SingleAsync(row => row.BookingId == id)).Status);
        Assert.Equal(1, await verify.TicketIssueOutboxMessages.CountAsync(row => row.BookingId == id));
    }

    [BookingIntegrationFact]
    public async Task Persisted_inbox_survives_lookup_outage_and_replays_after_booking_commit()
    {
        var gateway = new ResultGateway();
        var id = await CreateBookingAsync(gateway);
        var message = Success(gateway);
        await ReceiveAsync(message);
        gateway.Unavailable = true;
        await DispatchAsync(gateway);
        gateway.Unavailable = false;
        await RetryNowAsync();
        // Simulate a crash after confirmation commits but before the inbox completion marker.
        await using (var db = fixture.CreateContext())
            Assert.Equal("Succeeded", (await fixture.CreateService(db, gateway).ApplyPaymentResultAsync(
                await db.PaymentResultInboxMessages.AsNoTracking().SingleAsync(row => row.MessageId == message.MessageId))).PaymentStatus);
        await DispatchAsync(gateway);
        await using var verify = fixture.CreateContext();
        Assert.NotNull((await verify.PaymentResultInboxMessages.SingleAsync(row => row.MessageId == message.MessageId)).ProcessedAtUtc);
        Assert.Equal(1, await verify.TicketIssueOutboxMessages.CountAsync(row => row.BookingId == id));
    }

    private async Task<Guid> CreateBookingAsync(ResultGateway gateway, string outcome = "Succeeded")
    {
        var inventory = await fixture.CreateInventoryAsync();
        var id = await fixture.ReserveAsync(inventory, gateway);
        var now = DateTimeOffset.UtcNow;
        now = now.AddTicks(-(now.UtcTicks % 10));
        gateway.Current = new(Guid.NewGuid(), id, inventory.Price, "SEK", outcome,
            now.AddSeconds(-1), now, outcome == "Succeeded" ? now : null, null);
        await using var db = fixture.CreateContext();
        await db.Bookings.Where(row => row.BookingId == id).ExecuteUpdateAsync(update => update.SetProperty(row => row.PaymentRequested, true));
        return id;
    }

    private async Task ReceiveAsync(PaymentSucceededV1 message)
    {
        await using var db = fixture.CreateContext();
        await new PaymentResultInbox(db, TimeProvider.System).ReceiveAsync(message);
    }

    private async Task DispatchAsync(ResultGateway gateway)
    {
        await using var db = fixture.CreateContext();
        await new PaymentResultInboxDispatcher(db, fixture.CreateService(db, gateway), TimeProvider.System,
            NullLogger<PaymentResultInboxDispatcher>.Instance).DispatchPendingAsync();
    }

    private async Task RetryNowAsync()
    {
        await using var db = fixture.CreateContext();
        await db.PaymentResultInboxMessages.Where(row => row.ProcessedAtUtc == null && row.RejectedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.NextAttemptAtUtc, DateTimeOffset.UtcNow.AddSeconds(-1)));
    }

    private static PaymentSucceededV1 Success(ResultGateway gateway) => new()
    {
        MessageId = Guid.NewGuid(), CorrelationId = gateway.Current!.BookingId,
        PaymentId = gateway.Current.PaymentId, BookingId = gateway.Current.BookingId,
        Amount = gateway.Current.Amount, Currency = gateway.Current.Currency,
        PaidAtUtc = gateway.Current.PaidAt!.Value, OccurredAtUtc = gateway.Current.CompletedAt!.Value
    };

    private static PaymentFailedV1 Failure(ResultGateway gateway) => new()
    {
        MessageId = Guid.NewGuid(), CorrelationId = gateway.Current!.BookingId,
        PaymentId = gateway.Current.PaymentId, BookingId = gateway.Current.BookingId,
        Amount = gateway.Current.Amount, Currency = gateway.Current.Currency,
        FailureCode = gateway.Current.Status, OccurredAtUtc = gateway.Current.CompletedAt!.Value
    };

    private sealed class ResultGateway : IPaymentGateway
    {
        public PaymentResult? Current { get; set; }
        public bool Unavailable { get; set; }
        public bool FailFirstRefund { get; init; }
        public int RefundCalls { get; private set; }
        public Task<PaymentResult?> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken) =>
            Unavailable ? Task.FromException<PaymentResult?>(new HttpRequestException("Payment lookup unavailable.")) : Task.FromResult(Current);
        public Task<PaymentResult> ProcessAsync(Guid bookingId, decimal amount, string currency, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Result handling must never initiate another payment.");
        public Task<PaymentResult> RefundAsync(Guid paymentId, CancellationToken cancellationToken)
        {
            RefundCalls++;
            if (FailFirstRefund && RefundCalls == 1) throw new HttpRequestException("Refund unavailable.");
            Current = Current! with { RefundedAt = DateTimeOffset.UtcNow };
            return Task.FromResult(Current);
        }
        public Task<PaymentResult> CancelAsync(Guid bookingId, decimal amount, string currency, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Result handling already has an existing payment attempt.");
    }
}
