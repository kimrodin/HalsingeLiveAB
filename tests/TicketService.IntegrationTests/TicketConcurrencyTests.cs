using HalsingeLive.Contracts.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TicketService.Api;
using TicketService.Data;
using TicketService.Features.Tickets.DownloadTicket;
using TicketService.Features.Tickets.IssueTickets;
using TicketService.Features.Tickets.Models;

namespace TicketService.IntegrationTests;

public sealed class TicketConcurrencyTests(TicketDatabaseFixture database) : IClassFixture<TicketDatabaseFixture>
{
    [TicketIntegrationTheory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task ConcurrentIssuance_ConvergesWithoutDuplicatesAfterTransientRetry(int quantity, bool changedSnapshot)
    {
        var gate = new ConcurrentSaveGate();
        await using var firstDb = database.CreateContext(gate);
        await using var secondDb = database.CreateContext(gate);
        var message = new BookingConfirmedV1
        {
            MessageId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), OccurredAtUtc = DateTimeOffset.UtcNow,
            BookingId = Guid.NewGuid(), BookingItemId = Guid.NewGuid(), EventId = Guid.NewGuid(),
            EventName = "Concurrent event", EventStartsAtUtc = DateTimeOffset.Parse("2026-11-01T19:00:00Z"),
            VenueName = "Hall", SectionName = "Standing", Quantity = quantity, UnitPrice = 100m, Currency = "SEK"
        };
        var secondMessage = changedSnapshot ? message with { UnitPrice = 200m } : message;
        var firstTask = AttemptIssue(firstDb, message);
        var secondTask = AttemptIssue(secondDb, secondMessage);

        var results = await Task.WhenAll(firstTask, secondTask);

        // Multi-row inserts can deadlock when EF orders randomly generated ticket IDs differently.
        // Booking's durable outbox retries transient failures. Replay only that PostgreSQL failure,
        // after both original transactions have finished; unique violations must be handled by the service.
        for (var index = 0; index < results.Length; index++)
        {
            if (results[index].Error?.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected })
            {
                await using var retryDb = database.CreateContext();
                results[index] = await AttemptIssue(retryDb, index == 0 ? message : secondMessage);
            }
        }

        var winner = Assert.Single(results, r => r.Result is { AlreadyIssued: false }).Result!;
        if (changedSnapshot)
        {
            Assert.Single(results, r => r.Error is TicketIssueConflictException);
            Assert.Single(results, r => r.Result is not null);
        }
        else
        {
            Assert.All(results, r => Assert.Null(r.Error));
            var retry = Assert.Single(results, r => r.Result is { AlreadyIssued: true }).Result!;
            Assert.Equal(winner.Tickets.Select(t => t.TicketId), retry.Tickets.Select(t => t.TicketId));
        }
        await using var verification = database.CreateContext();
        var saved = await verification.Tickets.Where(t => t.BookingItemId == message.BookingItemId)
            .OrderBy(t => t.TicketNumberInBookingItem).ToArrayAsync();
        Assert.Equal(Enumerable.Range(1, quantity), saved.Select(t => t.TicketNumberInBookingItem));
        Assert.Equal(winner.Tickets.Select(t => t.TicketId), saved.Select(t => t.TicketId));
        Assert.All(saved, t => Assert.Equal(winner.Tickets[0].UnitPrice, t.UnitPrice));
    }

    [TicketIntegrationTheory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task ConcurrentStatusChanges_AllowExactlyOneAndPreserveWinner(bool firstCheckIn, bool secondCheckIn)
    {
        var ticket = new Ticket
        {
            TicketId = Guid.NewGuid(), BookingId = Guid.NewGuid(), BookingItemId = Guid.NewGuid(),
            TicketNumberInBookingItem = 1, EventId = Guid.NewGuid(), EventName = "Event",
            EventStartsAt = DateTimeOffset.UtcNow.AddDays(1), VenueName = "Hall", SectionName = "Standing",
            UnitPrice = 100m, Currency = "SEK", Status = TicketStatus.Valid, IssuedAt = DateTimeOffset.UtcNow
        };
        await using (var seed = database.CreateContext())
        {
            seed.Tickets.Add(ticket);
            await seed.SaveChangesAsync();
        }
        var gate = new ConcurrentSaveGate();
        await using var firstDb = database.CreateContext(gate);
        await using var secondDb = database.CreateContext(gate);

        var results = await Task.WhenAll(
            ChangeStatus(CreateController(firstDb), ticket.TicketId, firstCheckIn),
            ChangeStatus(CreateController(secondDb), ticket.TicketId, secondCheckIn));

        var success = Assert.Single(results.OfType<OkObjectResult>());
        var winner = Assert.IsType<TicketStatusChangeResponse>(success.Value);
        var conflict = Assert.Single(results.OfType<ConflictObjectResult>());
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(409, problem.Status);
        Assert.Contains(winner.Status, problem.Detail);
        await using var verification = database.CreateContext();
        var saved = await verification.Tickets.SingleAsync(t => t.TicketId == ticket.TicketId);
        Assert.Equal(winner.Status, saved.Status.ToString());
        if (saved.Status == TicketStatus.CheckedIn)
        {
            Assert.Equal(ToPostgresPrecision(winner.ChangedAt), saved.CheckedInAt);
            Assert.Null(saved.CancelledAt);
        }
        else
        {
            Assert.Equal(TicketStatus.Cancelled, saved.Status);
            Assert.Equal(ToPostgresPrecision(winner.ChangedAt), saved.CancelledAt);
            Assert.Null(saved.CheckedInAt);
        }
    }

    private static async Task<(IssueTicketsResult? Result, Exception? Error)> AttemptIssue(
        TicketDbContext db, BookingConfirmedV1 message)
    {
        try
        {
            return (await new IssueTicketsHandler(db, TimeProvider.System).HandleAsync(message), null);
        }
        catch (TicketIssueConflictException exception)
        {
            return (null, exception);
        }
        catch (Exception exception) when (
            exception.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected })
        {
            return (null, exception);
        }
    }

    // PostgreSQL stores timestamps to microseconds; .NET also represents 100ns ticks.
    private static DateTimeOffset ToPostgresPrecision(DateTimeOffset value) =>
        value.AddTicks(-(value.Ticks % 10));

    private static Task<IActionResult> ChangeStatus(TicketsController controller, Guid id, bool checkIn) =>
        checkIn ? controller.CheckInTicket(id, "test-key", default) : controller.CancelTicket(id, "test-key", default);

    private static TicketsController CreateController(TicketDbContext db) => new(
        db, new IssueTicketsHandler(db, TimeProvider.System), new TicketDocumentService(),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TicketService:InternalApiKey"] = "test-key"
        }).Build(), NullLogger<TicketsController>.Instance);
}
