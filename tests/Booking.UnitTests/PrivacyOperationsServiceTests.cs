using Booking.Data;
using Booking.Features.Bookings;
using Booking.Features.Payments.Gateway;
using Booking.Features.Payments.Requests;
using Booking.Features.Payments.Results;
using Booking.Features.Privacy;
using Booking.Features.Tickets;
using Booking.Features.Tickets.Issuance;
using Microsoft.EntityFrameworkCore;

namespace Booking.UnitTests;

public sealed class PrivacyOperationsServiceTests
{
    [Fact]
    public async Task PseudonymizeExpiredAsync_ClearsDirectIdentifiersAtTwoYearBoundaryOnlyOnce()
    {
        var now = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
        await using var db = CreateDbContext();
        var expired = CreateBooking(now.AddYears(-2).AddTicks(-1), "shared@example.com");
        var atBoundary = CreateBooking(now.AddYears(-2), "boundary@example.com");
        var recentInExpiredGroup = CreateBooking(now.AddYears(-2).AddTicks(1), "shared@example.com");
        var unrelatedRecent = CreateBooking(now.AddYears(-2).AddTicks(1), "recent@example.com");
        db.Bookings.AddRange(expired, atBoundary, recentInExpiredGroup, unrelatedRecent);
        await db.SaveChangesAsync();

        var service = new PrivacyOperationsService(db, null!, null!, null!, new FixedTimeProvider(now));
        var preview = await service.GetRetentionPreviewAsync(CancellationToken.None);

        Assert.Equal(2, preview.TotalCount);
        Assert.Equal(2, preview.Candidates.Count);
        Assert.Equal(3, await service.PseudonymizeExpiredAsync(CancellationToken.None));

        var expiredAfter = await db.Bookings.SingleAsync(item => item.BookingId == expired.BookingId);
        var boundaryAfter = await db.Bookings.SingleAsync(item => item.BookingId == atBoundary.BookingId);
        var recentAfter = await db.Bookings.SingleAsync(item => item.BookingId == recentInExpiredGroup.BookingId);
        var unrelatedRecentAfter = await db.Bookings.SingleAsync(item => item.BookingId == unrelatedRecent.BookingId);

        Assert.Empty(expiredAfter.CustomerName);
        Assert.Empty(expiredAfter.CustomerEmail);
        Assert.Null(expiredAfter.CustomerId);
        Assert.NotNull(expiredAfter.GuestAccessTokenHash);
        Assert.Equal(now, expiredAfter.PersonalDataPseudonymizedAtUtc);
        Assert.Empty(boundaryAfter.CustomerName);
        Assert.Empty(boundaryAfter.CustomerEmail);
        Assert.NotNull(boundaryAfter.GuestAccessTokenHash);
        Assert.Empty(recentAfter.CustomerName);
        Assert.Empty(recentAfter.CustomerEmail);
        Assert.Equal(expiredAfter.PseudonymizedCustomerId, recentAfter.PseudonymizedCustomerId);
        Assert.NotEmpty(unrelatedRecentAfter.CustomerName);
        Assert.NotEmpty(unrelatedRecentAfter.CustomerEmail);
        Assert.Null(unrelatedRecentAfter.PersonalDataPseudonymizedAtUtc);
        Assert.Equal(0, await service.PseudonymizeExpiredAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PseudonymizeAsync_PreservesBookingAndRelatedTransactionFields()
    {
        var now = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
        await using var db = CreateDbContext();
        var booking = CreateBooking(now.AddYears(-1));
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        var service = new PrivacyOperationsService(db, null!, null!, null!, new FixedTimeProvider(now));
        Assert.True(await service.PseudonymizeAsync(booking.BookingId, CancellationToken.None));

        var stored = await db.Bookings.SingleAsync();
        Assert.Equal(booking.BookingId, stored.BookingId);
        Assert.Equal(booking.Status, stored.Status);
        Assert.Equal(booking.TotalPrice, stored.TotalPrice);
        Assert.NotNull(stored.GuestAccessTokenHash);
        Assert.NotNull(stored.PseudonymizedCustomerId);
        Assert.NotNull(stored.PersonalDataPseudonymizedAtUtc);
        Assert.True(await service.PseudonymizeAsync(booking.BookingId, CancellationToken.None));
        Assert.Equal(now, stored.PersonalDataPseudonymizedAtUtc);
    }

    [Fact]
    public async Task PseudonymizeAsync_UpdatesEveryBookingWithSameNormalizedEmail()
    {
        var now = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
        await using var db = CreateDbContext();
        var first = CreateBooking(now.AddYears(-1), "shared@example.com");
        var second = CreateBooking(now.AddMonths(-2), " SHARED@example.com ");
        var unrelated = CreateBooking(now.AddMonths(-1), "other@example.com");
        db.Bookings.AddRange(first, second, unrelated);
        await db.SaveChangesAsync();

        var service = new PrivacyOperationsService(db, null!, null!, null!, new FixedTimeProvider(now));
        Assert.True(await service.PseudonymizeAsync(first.BookingId, CancellationToken.None));

        var storedFirst = await db.Bookings.SingleAsync(item => item.BookingId == first.BookingId);
        var storedSecond = await db.Bookings.SingleAsync(item => item.BookingId == second.BookingId);
        var storedUnrelated = await db.Bookings.SingleAsync(item => item.BookingId == unrelated.BookingId);
        Assert.Empty(storedFirst.CustomerName);
        Assert.Empty(storedSecond.CustomerEmail);
        Assert.Equal(storedFirst.PseudonymizedCustomerId, storedSecond.PseudonymizedCustomerId);
        Assert.NotNull(storedFirst.GuestAccessTokenHash);
        Assert.NotNull(storedSecond.GuestAccessTokenHash);
        Assert.Equal("Test Customer", storedUnrelated.CustomerName);
        Assert.Null(storedUnrelated.PseudonymizedCustomerId);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsBookingPaymentTicketsAndInboxOutboxMetadata()
    {
        var now = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
        await using var db = CreateDbContext();
        var booking = CreateBooking(now.AddYears(-1));
        db.Bookings.Add(booking);
        db.PaymentRequestOutboxMessages.Add(new PaymentRequestOutboxMessage
        {
            MessageId = Guid.NewGuid(),
            BookingId = booking.BookingId,
            Amount = booking.TotalPrice,
            Currency = "SEK",
            CreatedAtUtc = now.AddMinutes(-3),
            NextAttemptAtUtc = now.AddMinutes(-3),
            LastError = string.Empty
        });
        db.PaymentResultInboxMessages.Add(new PaymentResultInboxMessage
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = booking.BookingId,
            PaymentId = Guid.NewGuid(),
            BookingId = booking.BookingId,
            Amount = booking.TotalPrice,
            Currency = "SEK",
            Outcome = "Succeeded",
            OccurredAtUtc = now.AddMinutes(-2),
            ReceivedAtUtc = now.AddMinutes(-2),
            NextAttemptAtUtc = now.AddMinutes(-2),
            LastError = string.Empty
        });
        db.TicketIssueOutboxMessages.Add(new TicketIssueOutboxMessage
        {
            MessageId = Guid.NewGuid(),
            BookingId = booking.BookingId,
            BookingItemId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventName = "Test event",
            EventStartsAtUtc = now.AddDays(1),
            VenueName = "Test venue",
            SectionName = "Test section",
            Quantity = 1,
            UnitPrice = booking.TotalPrice,
            Currency = "SEK",
            CreatedAtUtc = now.AddMinutes(-1),
            NextAttemptAtUtc = now.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var payment = new PaymentResult(
            Guid.NewGuid(),
            booking.BookingId,
            booking.TotalPrice,
            "SEK",
            "Succeeded",
            now.AddMinutes(-4),
            now.AddMinutes(-2),
            now.AddMinutes(-2),
            null);
        var resultOutbox = new PaymentResultOutboxSummaryResponse(
            Guid.NewGuid(),
            payment.PaymentId,
            booking.BookingId,
            booking.TotalPrice,
            "SEK",
            "Succeeded",
            now.AddMinutes(-2),
            now.AddMinutes(-2),
            now.AddMinutes(-1),
            1);
        var service = new PrivacyOperationsService(
            db,
            new StubPaymentGateway(payment),
            new StubPaymentPrivacyDataClient([resultOutbox]),
            new StubTicketServiceClient(),
            new FixedTimeProvider(now));
        var result = await service.GetSummaryAsync(booking.BookingId, CancellationToken.None);

        Assert.Equal(booking.BookingId, result.BookingId);
        Assert.Equal(booking.CustomerName, result.CustomerName);
        Assert.Equal(booking.CustomerEmail, result.CustomerEmail);
        Assert.Equal(payment, result.Payment);
        Assert.Equal(resultOutbox, Assert.Single(result.PaymentResultOutbox));
        Assert.Equal(3, result.ProcessingRecords.Count);
        Assert.Contains(result.ManualReviewSources, source => source.Contains("RabbitMQ"));
    }

    private static BookingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BookingDbContext(options);
    }

    private static Booking.Features.Bookings.Models.Booking CreateBooking(
        DateTimeOffset createdAt,
        string email = "test@example.com") =>
        new()
        {
            BookingId = Guid.NewGuid(),
            CustomerId = "customer-123",
            CustomerName = "Test Customer",
            CustomerEmail = email,
            GuestAccessTokenHash = [1, 2, 3],
            Status = BookingStatuses.Confirmed,
            TotalPrice = 42.50m,
            CreatedAt = createdAt.UtcDateTime
        };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubPaymentGateway(PaymentResult payment) : IPaymentGateway
    {
        public Task<PaymentResult> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken) =>
            Task.FromResult(payment);

        public Task<PaymentResult> ProcessAsync(
            Guid bookingId, decimal amount, string currency, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<PaymentResult> RefundAsync(Guid paymentId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<PaymentResult> CancelAsync(
            Guid bookingId, decimal amount, string currency, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class StubPaymentPrivacyDataClient(
        IReadOnlyList<PaymentResultOutboxSummaryResponse> resultOutbox) : IPaymentPrivacyDataClient
    {
        public Task<IReadOnlyList<PaymentResultOutboxSummaryResponse>> GetResultOutboxAsync(
            Guid bookingId,
            CancellationToken cancellationToken) =>
            Task.FromResult(resultOutbox);
    }

    private sealed class StubTicketServiceClient : ITicketServiceClient
    {
        public Task IssueTicketsAsync(
            HalsingeLive.Contracts.Messaging.BookingConfirmed message,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<TicketServiceTicketResponse>> GetTicketsForBookingAsync(
            Guid bookingId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TicketServiceTicketResponse>>([]);

        public Task<byte[]> GetTicketPdfAsync(
            Guid bookingId,
            Guid ticketId,
            bool download,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<TicketServiceStatusChangeResponse> CheckInTicketAsync(
            Guid ticketId,
            Guid eventId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<TicketServiceStatusChangeResponse> CancelTicketAsync(
            Guid ticketId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
