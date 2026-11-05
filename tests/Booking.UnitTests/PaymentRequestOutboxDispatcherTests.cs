using Booking.Data;
using Booking.Features.Bookings;
using Booking.Features.Payments;
using HalsingeLive.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using BookingModel = Booking.Features.Bookings.Models.Booking;

namespace Booking.UnitTests;

public sealed class PaymentRequestOutboxDispatcherTests
{
    [Fact]
    public async Task Retry_after_uncertain_delivery_preserves_identity_and_snapshot()
    {
        await using var db = CreateDb();
        var message = await SeedAsync(db);
        var publisher = new RecordingPublisher { Fail = true };
        var dispatcher = new PaymentRequestOutboxDispatcher(db, publisher, TimeProvider.System,
            NullLogger<PaymentRequestOutboxDispatcher>.Instance);
        await dispatcher.DispatchPendingAsync();
        Assert.Null(message.DeliveredAtUtc);
        Assert.Equal(1, message.AttemptCount);
        Assert.Equal("Broker unavailable", message.LastError);
        Assert.True(message.NextAttemptAtUtc > message.CreatedAtUtc);
        var original = Assert.Single(publisher.Messages);

        // A later worker scope recovers the durable row rather than generating a new request.
        db.ChangeTracker.Clear();
        var retry = await db.PaymentRequestOutboxMessages.SingleAsync();
        retry.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
        publisher.Fail = false;
        await dispatcher.DispatchPendingAsync();
        Assert.Equal(original, publisher.Messages.Last());
        Assert.Equal(message.MessageId, original.MessageId);
        Assert.Equal(message.BookingId, original.CorrelationId);
        Assert.Equal(249.50m, original.Amount);
        Assert.Equal("SEK", original.Currency);
        Assert.Equal(message.CreatedAtUtc, original.OccurredAtUtc);
        Assert.NotNull(retry.DeliveredAtUtc);
        Assert.Null(retry.LastError);
    }

    [Theory]
    [InlineData(BookingStatuses.Expired, false)]
    [InlineData(BookingStatuses.Cancelled, false)]
    [InlineData(BookingStatuses.Confirmed, false)]
    [InlineData(BookingStatuses.Reserved, true)]
    public async Task Invalid_reservation_is_suppressed_instead_of_published(string status, bool expired)
    {
        await using var db = CreateDb();
        var message = await SeedAsync(db);
        var booking = await db.Bookings.SingleAsync();
        booking.Status = status;
        if (expired) booking.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
        var publisher = new RecordingPublisher();
        await new PaymentRequestOutboxDispatcher(db, publisher, TimeProvider.System,
            NullLogger<PaymentRequestOutboxDispatcher>.Instance).DispatchPendingAsync();
        Assert.Empty(publisher.Messages);
        Assert.NotNull(message.SuppressedAtUtc);
        Assert.Null(message.DeliveredAtUtc);
        Assert.False(booking.PaymentSettled); // HTTP reconciliation must still fence uncertain deliveries.
    }

    [Fact]
    public async Task Shutdown_during_publish_leaves_delivery_pending()
    {
        await using var db = CreateDb();
        var message = await SeedAsync(db);
        using var shutdown = new CancellationTokenSource();
        var publisher = new RecordingPublisher { Shutdown = shutdown };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PaymentRequestOutboxDispatcher(db, publisher,
            TimeProvider.System, NullLogger<PaymentRequestOutboxDispatcher>.Instance).DispatchPendingAsync(shutdown.Token));
        db.ChangeTracker.Clear();
        var saved = await db.PaymentRequestOutboxMessages.SingleAsync();
        Assert.Null(saved.DeliveredAtUtc);
        Assert.Null(saved.SuppressedAtUtc);
        Assert.Equal(0, saved.AttemptCount);
    }

    private static BookingDbContext CreateDb() => new(new DbContextOptionsBuilder<BookingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<PaymentRequestOutboxMessage> SeedAsync(BookingDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var message = new PaymentRequestOutboxMessage
        {
            MessageId = Guid.NewGuid(), BookingId = Guid.NewGuid(), Amount = 249.50m,
            Currency = "SEK", CreatedAtUtc = now, NextAttemptAtUtc = now
        };
        db.Bookings.Add(new BookingModel
        {
            BookingId = message.BookingId, Status = BookingStatuses.Reserved, PaymentRequested = true,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10), TotalPrice = message.Amount
        });
        db.PaymentRequestOutboxMessages.Add(message);
        await db.SaveChangesAsync();
        return message;
    }

    private sealed class RecordingPublisher : IPaymentRequestedPublisher
    {
        public List<PaymentRequestedV1> Messages { get; } = [];
        public bool Fail { get; set; }
        public CancellationTokenSource? Shutdown { get; init; }
        public Task PublishAsync(PaymentRequestedV1 message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            if (Shutdown is not null)
            {
                Shutdown.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }
            return Fail ? Task.FromException(new IOException("Broker unavailable")) : Task.CompletedTask;
        }
    }
}
