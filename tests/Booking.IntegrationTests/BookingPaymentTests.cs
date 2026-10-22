using Booking.Features.Bookings;
using Booking.Features.Payments;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using Inventory = Booking.Features.EventInventory.Models.EventInventory;

namespace Booking.IntegrationTests;

public sealed class BookingPaymentTests(BookingDatabaseFixture fixture) : IClassFixture<BookingDatabaseFixture>
{
    [BookingIntegrationFact]
    public async Task SuccessfulPayment_UsesDatabasePriceAndConfirmsExactlyOnce()
    {
        var gateway = new FakePaymentGateway();
        var inventory = await fixture.CreateInventoryAsync(price: 149.75m);
        var id = await fixture.ReserveAsync(inventory, gateway, quantity: 2);

        var result = await PayAsync(id, gateway);
        var repeated = await PayAsync(id, gateway);

        Assert.Equal(BookingStatuses.Confirmed, result.BookingStatus);
        Assert.Equal(BookingStatuses.Confirmed, repeated.BookingStatus);
        Assert.Equal("Succeeded", result.PaymentStatus);
        Assert.Equal(1, gateway.ProcessCalls);
        Assert.Equal(299.50m, gateway.Payments[id].Amount);
        Assert.Equal("SEK", gateway.Payments[id].Currency);
        Assert.Equal(8, await DatabaseStockAsync(inventory));
    }

    [BookingIntegrationFact]
    public async Task FailedPayment_CancelsBookingAndReleasesRedisReservation()
    {
        var gateway = new FakePaymentGateway { Status = "Failed" };
        var inventory = await fixture.CreateInventoryAsync();
        var id = await fixture.ReserveAsync(inventory, gateway, 3);

        var result = await PayAsync(id, gateway);

        Assert.Equal(BookingStatuses.Cancelled, result.BookingStatus);
        Assert.Equal("Failed", result.PaymentStatus);
        Assert.Equal(10, await DatabaseStockAsync(inventory));
        Assert.Equal(10, await fixture.Reservations.GetAvailableQuantityAsync(inventory.EventId, inventory.SectionId, 10));
    }

    [BookingIntegrationFact]
    public async Task SimulatorTimeout_DoesNotConfirmOrCreateAnotherPayment()
    {
        var gateway = new FakePaymentGateway { Status = "TimedOut" };
        var inventory = await fixture.CreateInventoryAsync();
        var id = await fixture.ReserveAsync(inventory, gateway);

        var result = await PayAsync(id, gateway);
        await PayAsync(id, gateway);

        Assert.NotEqual(BookingStatuses.Confirmed, result.BookingStatus);
        Assert.Equal("TimedOut", result.PaymentStatus);
        Assert.Equal(1, gateway.ProcessCalls);
        Assert.Equal(10, await DatabaseStockAsync(inventory));
    }

    [BookingIntegrationFact]
    public async Task ExpiredReservation_IsNotChargedOrConfirmed()
    {
        var gateway = new FakePaymentGateway();
        var inventory = await fixture.CreateInventoryAsync();
        var id = await fixture.ReserveAsync(inventory, gateway);
        await SetExpirationAsync(id, DateTime.UtcNow.AddSeconds(-1));

        var result = await PayAsync(id, gateway);

        Assert.Equal(BookingStatuses.Expired, result.BookingStatus);
        Assert.Equal(0, gateway.ProcessCalls);
        Assert.Equal(10, await DatabaseStockAsync(inventory));
    }

    [BookingIntegrationFact]
    public async Task InvalidPaymentIdentityAmountCurrencyOrStatus_NeverConfirms()
    {
        Func<PaymentResult, PaymentResult>[] invalidResults =
        [
            payment => payment with { BookingId = Guid.NewGuid() },
            payment => payment with { Amount = payment.Amount + 1m },
            payment => payment with { Currency = "EUR" },
            payment => payment with { Status = "UnexpectedStatus" },
            payment => payment with { PaymentId = Guid.Empty }
        ];
        foreach (var alter in invalidResults)
        {
            var gateway = new FakePaymentGateway { AlterResult = alter };
            var inventory = await fixture.CreateInventoryAsync();
            var id = await fixture.ReserveAsync(inventory, gateway);

            var result = await PayAsync(id, gateway);

            Assert.NotEqual(BookingStatuses.Confirmed, result.BookingStatus);
            Assert.Equal(10, await DatabaseStockAsync(inventory));
        }
    }

    [BookingIntegrationFact]
    public async Task ConcurrentRequests_OnlyOnePaymentIsSubmittedAndStockIsDeductedOnce()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new FakePaymentGateway
        {
            BeforeResult = async () => { entered.SetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        };
        var inventory = await fixture.CreateInventoryAsync();
        var id = await fixture.ReserveAsync(inventory, gateway);

        var first = PayAsync(id, gateway);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        StartPaymentResult second;
        try
        {
            second = await PayAsync(id, gateway).WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            release.TrySetResult();
        }
        var completed = await first;

        Assert.Equal(BookingStatuses.Confirmed, completed.BookingStatus);
        Assert.Contains(second.PaymentStatus, new[] { "Pending", "Succeeded" });
        Assert.Equal(1, gateway.ProcessCalls);
        Assert.Equal(9, await DatabaseStockAsync(inventory));
    }

    [BookingIntegrationFact]
    public async Task LostHttpResponse_IsRecoveredByLookupWithoutSubmittingAnotherCharge()
    {
        var gateway = new FakePaymentGateway { LoseFirstResponse = true };
        var inventory = await fixture.CreateInventoryAsync();
        var id = await fixture.ReserveAsync(inventory, gateway);

        var unknown = await PayAsync(id, gateway);
        Assert.NotEqual(BookingStatuses.Confirmed, unknown.BookingStatus);
        Assert.NotEqual("Failed", unknown.PaymentStatus);

        // A fresh context represents another request or recovery worker after restart.
        var recovered = await PayAsync(id, gateway);

        Assert.Equal(BookingStatuses.Confirmed, recovered.BookingStatus);
        Assert.Equal(1, gateway.ProcessCalls);
        Assert.True(gateway.LookupCalls > 0);
        Assert.Equal(9, await DatabaseStockAsync(inventory));
    }

    [BookingIntegrationFact]
    public async Task ExpiredUnknownAttemptWithoutPayment_IsDurablyFencedWithoutAnotherCharge()
    {
        var gateway = new FakePaymentGateway { RejectFirstRequestBeforeSaving = true };
        var inventory = await fixture.CreateInventoryAsync();
        var id = await fixture.ReserveAsync(inventory, gateway);
        var unknown = await PayAsync(id, gateway);
        Assert.Equal("Unknown", unknown.PaymentStatus);
        await SetExpirationAsync(id, DateTime.UtcNow.AddSeconds(-1));

        var recovered = await PayAsync(id, gateway);

        Assert.NotEqual(BookingStatuses.Confirmed, recovered.BookingStatus);
        Assert.Equal(1, gateway.ProcessCalls);
        Assert.Equal(1, gateway.CancelCalls);
        Assert.NotNull(gateway.Payments[id].RefundedAt);
        Assert.Equal(10, await DatabaseStockAsync(inventory));
    }

    [BookingIntegrationFact]
    public async Task CancellationWhilePaymentIsInFlight_RefundsTheCharge()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new FakePaymentGateway
        {
            BeforeResult = async () => { entered.SetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        };
        var inventory = await fixture.CreateInventoryAsync();
        var id = await fixture.ReserveAsync(inventory, gateway);
        var payment = PayAsync(id, gateway);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await using var db = fixture.CreateContext();
            var cancelled = await fixture.CreateService(db, gateway).CancelAsync(id);
            Assert.Equal(BookingActionStatus.Completed, cancelled.Status);
        }
        finally
        {
            release.TrySetResult();
        }

        var result = await payment;

        Assert.Equal(BookingStatuses.Cancelled, result.BookingStatus);
        Assert.NotNull(gateway.Payments[id].RefundedAt);
        Assert.Equal(1, gateway.RefundCalls);
        Assert.Equal(10, await DatabaseStockAsync(inventory));
    }

    [BookingIntegrationFact]
    public async Task PaymentFinishingAfterExpiration_IsRefundedAndNeverConfirmed()
    {
        var gateway = new FakePaymentGateway();
        var inventory = await fixture.CreateInventoryAsync();
        var id = await fixture.ReserveAsync(inventory, gateway);
        // Expire the reservation while the remote operation is executing. This avoids
        // relying on machine speed or sleeping through a short reservation deadline.
        gateway.BeforeResult = () => SetExpirationAsync(id, DateTime.UtcNow.AddSeconds(-1));

        var result = await PayAsync(id, gateway);

        Assert.NotEqual(BookingStatuses.Confirmed, result.BookingStatus);
        Assert.NotNull(gateway.Payments[id].RefundedAt);
        Assert.Equal(1, gateway.RefundCalls);
        Assert.Equal(10, await DatabaseStockAsync(inventory));
    }

    [BookingIntegrationFact]
    public async Task PostgresRejectsOversellingEvenWhenRedisHasExcessStock()
    {
        var gateway = new FakePaymentGateway();
        var inventory = await fixture.CreateInventoryAsync(stock: 1);
        var firstId = await fixture.ReserveAsync(inventory, gateway);
        // Simulate a stale Redis inventory cache. PostgreSQL must remain authoritative.
        await fixture.Redis.GetDatabase().StringSetAsync($"inventory:{inventory.EventId:N}:{inventory.SectionId:N}:available", 1);
        var secondId = await fixture.ReserveAsync(inventory, gateway);

        var results = await Task.WhenAll(PayAsync(firstId, gateway), PayAsync(secondId, gateway));

        Assert.Single(results, result => result.BookingStatus == BookingStatuses.Confirmed);
        Assert.Equal(0, await DatabaseStockAsync(inventory));
        Assert.Equal(1, gateway.RefundCalls);
    }

    private async Task<StartPaymentResult> PayAsync(Guid id, IPaymentGateway gateway)
    {
        await using var db = fixture.CreateContext();
        return await fixture.CreateService(db, gateway).StartPaymentAsync(id);
    }

    private async Task SetExpirationAsync(Guid id, DateTime expiresAt)
    {
        await using var db = fixture.CreateContext();
        await db.Bookings.Where(booking => booking.BookingId == id).ExecuteUpdateAsync(update =>
            update.SetProperty(booking => booking.ExpiresAt, expiresAt));
    }

    private async Task<int> DatabaseStockAsync(Inventory inventory)
    {
        await using var db = fixture.CreateContext();
        return await db.EventInventory.Where(row => row.InventoryId == inventory.InventoryId)
            .Select(row => row.AvailableQuantity).SingleAsync();
    }

    private sealed class FakePaymentGateway : IPaymentGateway
    {
        private int processCalls;
        private int lookupCalls;
        private int refundCalls;
        private int cancelCalls;
        public int ProcessCalls => processCalls;
        public int LookupCalls => lookupCalls;
        public int RefundCalls => refundCalls;
        public int CancelCalls => cancelCalls;
        public string Status { get; init; } = "Succeeded";
        public bool LoseFirstResponse { get; init; }
        public bool RejectFirstRequestBeforeSaving { get; init; }
        public Func<PaymentResult, PaymentResult>? AlterResult { get; init; }
        public Func<Task>? BeforeResult { get; set; }
        public ConcurrentDictionary<Guid, PaymentResult> Payments { get; } = new();

        public Task<PaymentResult?> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref lookupCalls);
            return Task.FromResult(Payments.TryGetValue(bookingId, out var result) ? result : null);
        }

        public async Task<PaymentResult> ProcessAsync(Guid bookingId, decimal amount, string currency, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref processCalls);
            if (RejectFirstRequestBeforeSaving && call == 1)
                throw new HttpRequestException("The request could not reach the payment service.");
            if (BeforeResult is not null) await BeforeResult();
            var result = new PaymentResult(Guid.NewGuid(), bookingId, amount, currency, Status,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
            result = AlterResult?.Invoke(result) ?? result;
            Payments[bookingId] = result;
            if (LoseFirstResponse && call == 1)
                throw new HttpRequestException("The payment was saved but the HTTP response was lost.");
            return result;
        }

        public Task<PaymentResult> RefundAsync(Guid paymentId, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref refundCalls);
            var stored = Payments.Single(pair => pair.Value.PaymentId == paymentId);
            var result = stored.Value with { RefundedAt = DateTimeOffset.UtcNow };
            Payments[stored.Key] = result;
            return Task.FromResult(result);
        }

        public Task<PaymentResult> CancelAsync(Guid bookingId, decimal amount, string currency, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref cancelCalls);
            var result = new PaymentResult(Guid.NewGuid(), bookingId, amount, currency, "Failed",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            Payments[bookingId] = result;
            return Task.FromResult(result);
        }
    }
}
