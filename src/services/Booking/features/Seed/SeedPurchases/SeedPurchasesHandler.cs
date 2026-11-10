using Booking.Data;
using Booking.Features.BookingHolds;
using Booking.Features.Bookings;
using Booking.Features.Bookings.Models;
using Booking.Features.EventCatalog;
using Booking.Features.Tickets.Issuance;
using Microsoft.EntityFrameworkCore;
using BookingModel = Booking.Features.Bookings.Models.Booking;

namespace Booking.Features.Seed.SeedPurchases;

public sealed class SeedPurchasesHandler(
    BookingDbContext db, RedisReservationService reservations, IEventSnapshotProvider catalog,
    ISeedPaymentsClient payments)
{
    public async Task<SeedPurchasesResponse> Handle(Guid[] eventIds, CancellationToken ct)
    {
        if (eventIds is null || eventIds.Length is < 1 or > 40 || eventIds.Distinct().Count() != eventIds.Length
            || eventIds.Any(id => !Enumerable.Range(1, 40).Any(ordinal => EventId(ordinal) == id)))
            throw new ArgumentException("Only the catalog's deterministic seed events can be seeded.");
        var createdBookings = 0;
        var createdTickets = 0;
        var totalBookings = 0;
        var totalTickets = 0;
        foreach (var eventId in eventIds.Order())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(51000000)", ct);
            var inventory = await db.EventInventory.Where(item => item.EventId == eventId).ToArrayAsync(ct);
            if (inventory.Length == 0)
                throw new SeedPurchasesConflictException($"Seed inventory for {eventId} has not been provisioned. Run the seed again.");
            var sales = new List<SeedPayment>();
            var queued = await db.TicketIssueOutboxMessages.Where(item => item.EventId == eventId)
                .ToDictionaryAsync(item => item.BookingItemId, ct);
            foreach (var stock in inventory.OrderBy(item => item.SectionId))
            {
                var snapshot = await catalog.GetSnapshotAsync(stock.EventId, stock.SectionId, ct);
                var ordinal = int.Parse(eventId.ToString()[^12..]);
                var section = int.Parse(stock.SectionId.ToString()[^12..]);
                var sold = Math.Min(stock.TotalQuantity, Math.Max(8, stock.TotalQuantity * (35 + (ordinal * 7 + section * 11) % 50) / 100));
                // The regular ticket-issuance contract permits at most ten tickets per purchase.
                var batches = (sold + 7) / 8;
                for (var batch = 0; batch < batches; batch++)
                {
                    var quantity = Math.Min(8, sold - batch * 8);
                    var bookingId = Guid.Parse($"51000000-{ordinal:D4}-{section:D4}-0000-{batch + 1:D12}");
                    var itemId = Guid.Parse(bookingId.ToString().Replace("51000000-", "52000000-"));
                    var paidAt = snapshot.EventStartsAt.ToUniversalTime().AddDays(-42 + batch % 8 * 5).AddHours(-(batch / 8 % 12));
                    var amount = quantity * stock.Price;
                    var existing = await db.Bookings.Include(row => row.Items)
                        .SingleOrDefaultAsync(row => row.BookingId == bookingId, ct);
                    if (existing is not null)
                    {
                        if (existing.Status != BookingStatuses.Confirmed || existing.TotalPrice != amount
                            || existing.CreatedAt != paidAt.UtcDateTime || !existing.PaymentSettled
                            || existing.Items.Count != 1 || existing.Items[0].BookingItemId != itemId
                            || existing.Items[0].InventoryId != stock.InventoryId || existing.Items[0].Quantity != quantity
                            || existing.Items[0].UnitPrice != stock.Price)
                            throw new SeedPurchasesConflictException($"Seed booking {bookingId} has been changed.");
                    }
                    else
                    {
                        if (stock.AvailableQuantity < quantity)
                            throw new SeedPurchasesConflictException("There are not enough available places for the seed purchases.");
                        stock.AvailableQuantity -= quantity;
                        db.Bookings.Add(new BookingModel
                        {
                            BookingId = bookingId, CustomerName = $"Seedkund {batch + 1}",
                            CustomerEmail = $"seed-{ordinal}-{batch + 1}@example.test",
                            Status = BookingStatuses.Confirmed, TotalPrice = amount, CreatedAt = paidAt.UtcDateTime,
                            PaymentRequested = true, PaymentSettled = true,
                            Items = [new BookingItem
                            {
                                BookingItemId = itemId, InventoryId = stock.InventoryId, Quantity = quantity,
                                UnitPrice = stock.Price, Currency = "SEK", EventName = snapshot.EventName,
                                EventStartsAt = snapshot.EventStartsAt, VenueName = snapshot.VenueName, SectionName = snapshot.SectionName,
                            }],
                        });
                        createdBookings++;
                        createdTickets += quantity;
                    }
                    if (queued.TryGetValue(itemId, out var message))
                    {
                        message.DeliveredAtUtc = null;
                        message.NextAttemptAtUtc = DateTimeOffset.UtcNow;
                        message.AttemptCount = 0;
                        message.LastError = null;
                    }
                    else
                    {
                        db.TicketIssueOutboxMessages.Add(new TicketIssueOutboxMessage
                        {
                            MessageId = Guid.Parse(bookingId.ToString().Replace("51000000-", "53000000-")),
                            BookingId = bookingId, BookingItemId = itemId, EventId = eventId,
                            EventName = snapshot.EventName, EventStartsAtUtc = snapshot.EventStartsAt,
                            VenueName = snapshot.VenueName, SectionName = snapshot.SectionName,
                            Quantity = quantity, UnitPrice = stock.Price, Currency = "SEK",
                            CreatedAtUtc = paidAt, NextAttemptAtUtc = DateTimeOffset.UtcNow,
                        });
                    }
                    sales.Add(new(bookingId, amount, paidAt));
                    totalBookings++;
                    totalTickets += quantity;
                }
            }
            // External writes are idempotent. If Booking rolls back after payment creation,
            // a retry reuses exactly the same payment IDs, amounts and historical timestamps.
            await payments.EnsurePayments(sales, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            // Seed tools run without concurrent customer reservations. Rebuild the
            // cache from committed stock instead of simulating holds and compensations.
            foreach (var stock in inventory)
            {
                await reservations.DeleteInventoryAsync(stock.EventId, stock.SectionId);
                await reservations.InitializeInventoryAsync(stock.EventId, stock.SectionId, stock.AvailableQuantity);
            }
        }
        return new(createdBookings, createdTickets, totalBookings, totalTickets);
    }

    public static Guid EventId(int ordinal) => Guid.Parse($"10000000-0000-0000-0000-{ordinal:D12}");
}

public sealed record SeedPurchasesResponse(int CreatedBookings, int CreatedTickets, int TotalBookings, int TotalTickets);
public sealed class SeedPurchasesConflictException(string message) : Exception(message);
