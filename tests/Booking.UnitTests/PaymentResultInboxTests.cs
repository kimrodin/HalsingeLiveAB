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
