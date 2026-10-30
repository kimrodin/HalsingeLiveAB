using System.Net.Http.Json;
using Booking.Data;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.Bookings;

public sealed class VenueOccupancyReport(BookingDbContext db, HttpClient catalog)
{
    public async Task<IReadOnlyList<VenueOccupancyRow>> Get(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var totals = await db.EventInventory.AsNoTracking()
            .GroupBy(item => item.EventId)
            .Select(group => new InventoryTotal(group.Key,
                group.Sum(item => (long)item.TotalQuantity),
                group.Sum(item => (long)item.TotalQuantity - item.AvailableQuantity),
                group.Sum(item => item.TotalQuantity <= 0 || item.AvailableQuantity < 0
                    || item.AvailableQuantity > item.TotalQuantity ? 1 : 0)))
            .ToArrayAsync(ct);
        if (totals.Length == 0) return [];
        if (totals.Any(item => item.InvalidInventoryCount != 0))
            throw new InvalidOperationException("Inventory contains invalid capacity or availability values.");

        var events = new List<EventInfo>();
        foreach (var ids in totals.Select(item => item.EventId).Chunk(500))
        {
            using var response = await catalog.PostAsJsonAsync("GetEvents",
                new GetEventsRequest(ids, from?.Date, to?.Date), ct);
            response.EnsureSuccessStatusCode();
            var batch = await response.Content.ReadFromJsonAsync<GetEventsResponse>(cancellationToken: ct)
                ?? throw new HttpRequestException("EventCatalog returned no event data.");
            if (batch.MissingEventIds.Length != 0)
                throw new InvalidDataException($"EventCatalog is missing {batch.MissingEventIds.Length} requested event(s).");
            events.AddRange(batch.Events);
        }
        var byId = events.ToDictionary(item => item.EventId);
        var eventRows = totals.Where(total => byId.ContainsKey(total.EventId))
            .Select(total => (Total: total, Event: byId[total.EventId]))
            .Select(item => new EventOccupancyRow(item.Event.VenueId, item.Event.VenueName,
                item.Total.Capacity, item.Total.Sold,
                item.Total.Capacity == 0 ? 0 : Math.Round(100m * item.Total.Sold / item.Total.Capacity, 2)))
            .ToArray();

        return eventRows.GroupBy(item => new { item.VenueId, item.VenueName })
            .Select(group =>
            {
                var capacity = group.Sum(item => item.Capacity);
                var sold = group.Sum(item => item.Sold);
                return new VenueOccupancyRow(group.Key.VenueId, group.Key.VenueName,
                    capacity, sold,
                    capacity == 0 ? 0 : Math.Round(100m * sold / capacity, 2));
            })
            .OrderBy(item => item.VenueName)
            .ToArray();
    }

    private sealed record InventoryTotal(Guid EventId, long Capacity, long Sold, int InvalidInventoryCount);
    private sealed record EventInfo(Guid EventId, string Name, DateTime StartDate, Guid VenueId, string VenueName);
    private sealed record GetEventsResponse(EventInfo[] Events, Guid[] MissingEventIds);
    private sealed record GetEventsRequest(Guid[] EventIds, DateTime? From, DateTime? To);
    private sealed record EventOccupancyRow(Guid VenueId, string VenueName, long Capacity, long Sold,
        decimal OccupancyPercent);
}

public sealed record VenueOccupancyRow(Guid VenueId, string VenueName, long Capacity,
    long Sold, decimal OccupancyPercent);
