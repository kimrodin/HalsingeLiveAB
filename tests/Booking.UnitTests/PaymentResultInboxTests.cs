using Booking.Data;
using Booking.Features.Bookings;
using Booking.Features.Payments.Results;
using HalsingeLive.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;
using BookingModel = Booking.Features.Bookings.Models.Booking;

namespace Booking.UnitTests;

public sealed class PaymentResultInboxTests
{
    [Theory]
    [InlineData("Succeeded", 0, true)]
    [InlineData("Failed", 0, true)]
    [InlineData("TimedOut", 0, true)]
    [InlineData("Succeeded", -1, false)]
    [InlineData("Failed", -1, false)]
    public async Task Zero_amount_results_are_saved_but_negative_amounts_are_rejected(
        string outcome, int amount, bool accepted)
    {
        await using var db = new BookingDbContext(new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var bookingId = Guid.NewGuid();
        db.Bookings.Add(new BookingModel
        {
            BookingId = bookingId, Status = BookingStatuses.Reserved,
            PaymentRequested = true, TotalPrice = amount
        });
        await db.SaveChangesAsync();
        var inbox = new PaymentResultInbox(db, TimeProvider.System);
        var now = DateTimeOffset.UtcNow;
        Task Receive() => outcome == "Succeeded"
            ? inbox.ReceiveAsync(new PaymentSucceeded
            {
                MessageId = Guid.NewGuid(), CorrelationId = bookingId, BookingId = bookingId,
                PaymentId = Guid.NewGuid(), Amount = amount, Currency = "SEK",
                PaidAtUtc = now, OccurredAtUtc = now
            })
            : inbox.ReceiveAsync(new PaymentFailed
            {
                MessageId = Guid.NewGuid(), CorrelationId = bookingId, BookingId = bookingId,
                PaymentId = Guid.NewGuid(), Amount = amount, Currency = "SEK",
                FailureCode = outcome, OccurredAtUtc = now
            });

        if (accepted)
        {
            await Receive();
            var saved = Assert.Single(await db.PaymentResultInboxMessages.ToArrayAsync());
            Assert.Equal(0m, saved.Amount);
            Assert.Equal(outcome, saved.Outcome);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidDataException>(Receive);
            Assert.False(await db.PaymentResultInboxMessages.AnyAsync());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Mismatched_correlation_is_rejected_without_saving(bool succeeded)
    {
        await using var db = new BookingDbContext(new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var bookingId = Guid.NewGuid();
        db.Bookings.Add(new BookingModel
        {
            BookingId = bookingId, Status = BookingStatuses.Reserved,
            PaymentRequested = true, TotalPrice = 125.50m
        });
        await db.SaveChangesAsync();
        var inbox = new PaymentResultInbox(db, TimeProvider.System);
        var now = DateTimeOffset.UtcNow;

        await Assert.ThrowsAsync<InvalidDataException>(() => succeeded
            ? inbox.ReceiveAsync(new PaymentSucceeded
            {
                MessageId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), BookingId = bookingId,
                PaymentId = Guid.NewGuid(), Amount = 125.50m, Currency = "SEK",
                PaidAtUtc = now, OccurredAtUtc = now
            })
            : inbox.ReceiveAsync(new PaymentFailed
            {
                MessageId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), BookingId = bookingId,
                PaymentId = Guid.NewGuid(), Amount = 125.50m, Currency = "SEK",
                FailureCode = "Failed", OccurredAtUtc = now
            }));

        Assert.False(await db.PaymentResultInboxMessages.AnyAsync());
    }
}
