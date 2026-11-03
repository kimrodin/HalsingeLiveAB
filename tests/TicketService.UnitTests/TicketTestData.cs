using HalsingeLive.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TicketService.Api;
using TicketService.Data;
using TicketService.Features.Tickets.DownloadTicket;
using TicketService.Features.Tickets.IssueTickets;
using TicketService.Features.Tickets.Models;

namespace TicketService.UnitTests;

internal static class TicketTestData
{
    public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-01T12:00:00Z");
    public static TimeProvider Clock { get; } = new FixedTimeProvider();

    public static TicketDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<TicketDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public static TicketsController CreateController(TicketDbContext db, string? configuredKey = "test-key") => new(
        db, new IssueTicketsHandler(db, Clock), new TicketDocumentService(),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TicketService:InternalApiKey"] = configuredKey
        }).Build(), NullLogger<TicketsController>.Instance);

    public static BookingConfirmedV1 CreateMessage(int quantity = 2) => new()
    {
        MessageId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), OccurredAtUtc = Now,
        BookingId = Guid.NewGuid(), BookingItemId = Guid.NewGuid(), EventId = Guid.NewGuid(),
        EventName = "Live event", EventStartsAtUtc = DateTimeOffset.Parse("2026-11-01T19:00:00Z"),
        VenueName = "Main Hall", SectionName = "Standing", Quantity = quantity,
        UnitPrice = 249.50m, Currency = "SEK"
    };

    public static Ticket CreateTicket() => new()
    {
        TicketId = Guid.NewGuid(), BookingId = Guid.NewGuid(), BookingItemId = Guid.NewGuid(),
        TicketNumberInBookingItem = 1, EventId = Guid.NewGuid(), EventName = "Testkonsert",
        EventStartsAt = DateTimeOffset.Parse("2026-11-01T18:00:00Z"), VenueName = "Hälsingehallen",
        SectionName = "Ståplats", UnitPrice = 249.50m, Currency = "SEK",
        Status = TicketStatus.Valid, IssuedAt = Now
    };

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
