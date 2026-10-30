using System.Net.Http.Json;
using Booking.Data;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.Bookings;

public sealed class VenueOccupancyReport(BookingDbContext db, HttpClient catalog)
{
    public async Task<IReadOnlyList<VenueOccupancyRow>> Get(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var inventory = await db.EventInventory.AsNoTracking().ToListAsync(ct);
        var events = inventory.Select(x => x.EventId).Distinct().ToArray();
        var rows = new List<VenueOccupancyRow>();
        foreach (var eventId in events)
        {
            var eventInfo = await catalog.GetFromJsonAsync<EventInfo>($"GetEvent/{eventId}", ct)
                ?? throw new HttpRequestException("EventCatalog returned no event.");
            if (from.HasValue && eventInfo.StartDate.Date < from.Value.Date
                || to.HasValue && eventInfo.StartDate.Date > to.Value.Date) continue;
            var venue = await catalog.GetFromJsonAsync<VenueInfo>($"GetVenue/{eventInfo.VenueId}", ct)
                ?? throw new HttpRequestException("EventCatalog returned no venue.");
            var sections = inventory.Where(x => x.EventId == eventId).ToArray();
            var total = sections.Sum(x => x.TotalQuantity);
            var sold = sections.Sum(x => x.TotalQuantity - x.AvailableQuantity);
            rows.Add(new(eventId, eventInfo.Name, eventInfo.StartDate, venue.VenueId, venue.Name,
                total, sold, total == 0 ? 0 : Math.Round(100m * sold / total, 2)));
        }
        return rows.OrderBy(x => x.EventStartsAt).ThenBy(x => x.VenueName).ToArray();
    }

    private sealed record EventInfo(Guid EventId, string Name, DateTime StartDate, Guid VenueId);
    private sealed record VenueInfo(Guid VenueId, string Name);
}

public sealed record VenueOccupancyRow(Guid EventId, string EventName, DateTime EventStartsAt,
    Guid VenueId, string VenueName, int Capacity, int Sold, decimal OccupancyPercent);
