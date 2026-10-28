using Booking.Data;
using Booking.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Booking.UnitTests;

public sealed class TicketIssueOutboxDispatcherTests
{
    [Fact]
    public async Task DispatchPendingAsync_SendsSnapshotAndMarksMessageDelivered()
    {
        await using var dbContext = CreateDbContext();
        var outboxMessage = CreateOutboxMessage();
        dbContext.TicketIssueOutboxMessages.Add(outboxMessage);
        await dbContext.SaveChangesAsync();
        var client = new RecordingTicketServiceClient();
        var dispatcher = CreateDispatcher(dbContext, client);

        await dispatcher.DispatchPendingAsync();

        var savedMessage = await dbContext.TicketIssueOutboxMessages
            .SingleAsync(message => message.MessageId == outboxMessage.MessageId);
        Assert.NotNull(savedMessage.DeliveredAtUtc);
        Assert.Null(savedMessage.LastError);
        Assert.NotNull(client.Message);
        Assert.Equal(outboxMessage.MessageId, client.Message.CorrelationId);
        Assert.Equal(outboxMessage.BookingId, client.Message.BookingId);
        Assert.Equal(outboxMessage.BookingItemId, client.Message.BookingItemId);
        Assert.Equal(outboxMessage.EventName, client.Message.EventName);
        Assert.Equal(outboxMessage.EventStartsAtUtc, client.Message.EventStartsAtUtc);
        Assert.Equal(outboxMessage.Quantity, client.Message.Quantity);
    }

    [Fact]
    public async Task DispatchPendingAsync_PreservesMessageAndSchedulesRetryAfterFailure()
    {
        await using var dbContext = CreateDbContext();
        var outboxMessage = CreateOutboxMessage();
        var originalNextAttemptAtUtc = outboxMessage.NextAttemptAtUtc;
        dbContext.TicketIssueOutboxMessages.Add(outboxMessage);
        await dbContext.SaveChangesAsync();
        var dispatcher = CreateDispatcher(
            dbContext,
            new RecordingTicketServiceClient(new HttpRequestException("TicketService unavailable.")));

        await dispatcher.DispatchPendingAsync();

        var savedMessage = await dbContext.TicketIssueOutboxMessages
            .SingleAsync(message => message.MessageId == outboxMessage.MessageId);
        Assert.Null(savedMessage.DeliveredAtUtc);
        Assert.Equal(1, savedMessage.AttemptCount);
        Assert.Equal("TicketService unavailable.", savedMessage.LastError);
        Assert.True(savedMessage.NextAttemptAtUtc > originalNextAttemptAtUtc);
    }

    private static TicketIssueOutboxDispatcher CreateDispatcher(
        BookingDbContext dbContext,
        ITicketServiceClient client) =>
        new(
            dbContext,
            client,
            TimeProvider.System,
            NullLogger<TicketIssueOutboxDispatcher>.Instance);

    private static BookingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BookingDbContext(options);
    }

    private static TicketIssueOutboxMessage CreateOutboxMessage()
    {
        var now = DateTimeOffset.UtcNow;
        return new TicketIssueOutboxMessage
        {
            MessageId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            BookingItemId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventName = "Snapshot concert",
            EventStartsAtUtc = now.AddDays(10),
            VenueName = "Snapshot hall",
            SectionName = "Standing",
            Quantity = 2,
            UnitPrice = 199.50m,
            Currency = "SEK",
            CreatedAtUtc = now,
            NextAttemptAtUtc = now
        };
    }

    private sealed class RecordingTicketServiceClient(Exception? exception = null) : ITicketServiceClient
    {
        public BookingConfirmedMessage? Message { get; private set; }

        public Task IssueTicketsAsync(
            BookingConfirmedMessage message,
            CancellationToken cancellationToken = default)
        {
            Message = message;
            return exception is null ? Task.CompletedTask : Task.FromException(exception);
        }

        public Task<IReadOnlyList<TicketServiceTicketResponse>> GetTicketsForBookingAsync(
            Guid bookingId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TicketServiceTicketResponse>>([]);

        public Task<byte[]> GetTicketPdfAsync(
            Guid bookingId,
            Guid ticketId,
            bool download,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<byte>());
    }
}
