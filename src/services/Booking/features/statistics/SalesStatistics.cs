using Booking.Data;
using Booking.Features.Bookings;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.Statistics;

public sealed class SalesStatistics(BookingDbContext db, IStatisticsPayments payments)
{
    public async Task<SalesStatisticsResponse> Get(SalesPeriod period, CancellationToken cancellationToken)
    {
        var previous = period.Previous;
        var currentDays = Days(period);
        var previousDays = Days(previous);
        var from = new DateTimeOffset(previous.StartUtc);
        var to = new DateTimeOffset(period.EndUtc);
        // The payment service uses its PaidAt index to return only these two periods.
        // Booking then looks up at most 500 matching IDs, never its entire history.
        await foreach (var batch in payments.GetAsync(from, to, cancellationToken))
        {
            var relevant = batch.Where(p => p.PaidAt >= from && p.PaidAt < to).ToArray();
            var ids = relevant.Select(p => p.BookingId).ToArray();
            var bookings = await db.Bookings.AsNoTracking()
                .Where(b => ids.Contains(b.BookingId) && b.Status == BookingStatuses.Confirmed)
                .Select(b => new ConfirmedSale(b.BookingId, b.TotalPrice, b.Items.Sum(i => (long)i.Quantity)))
                .ToArrayAsync(cancellationToken);
            var byBooking = relevant.ToDictionary(p => p.BookingId);
            foreach (var booking in bookings)
            {
                if (!byBooking.TryGetValue(booking.BookingId, out var payment)
                    || payment.PaymentId == Guid.Empty || payment.Status != "Succeeded"
                    || payment.PaidAt is null || payment.Currency != "SEK"
                    || payment.Amount != booking.Amount || booking.Tickets <= 0
                    || payment.RefundedAt < payment.PaidAt)
                    throw new InvalidDataException("Confirmed booking and payment data do not match.");

                var date = SalesPeriod.LocalDate(payment.PaidAt.Value);
                var days = date >= period.From && date <= period.To ? currentDays
                    : date >= previous.From && date <= previous.To ? previousDays : null;
                if (days is null) continue;
                var day = days[date];
                day.GrossRevenue += payment.Amount;
                if (payment.RefundedAt is not null)
                {
                    // Full refunds are attributed to the original Swedish sale date.
                    day.RefundedRevenue += payment.Amount;
                    day.RefundedTickets += booking.Tickets;
                }
                else day.SoldTickets += booking.Tickets;
            }
        }

        var current = Summary(currentDays.Values);
        var prior = Summary(previousDays.Values);
        return new(period.From, period.To, "SEK", SalesPeriod.TimeZoneId, current,
            new(previous.From, previous.To, prior),
            new(Change(current.NetRevenue, prior.NetRevenue), Change(current.SoldTickets, prior.SoldTickets)),
            currentDays.Values.Select(d => new DailySales(d.Date, d.GrossRevenue, d.RefundedRevenue,
                d.GrossRevenue - d.RefundedRevenue, d.SoldTickets, d.RefundedTickets)).ToArray());
    }

    private static SortedDictionary<DateOnly, Day> Days(SalesPeriod period) => new(
        Enumerable.Range(0, period.Days).ToDictionary(i => period.From.AddDays(i), i => new Day(period.From.AddDays(i))));
    private static SalesSummary Summary(IEnumerable<Day> days) => new(
        days.Sum(d => d.GrossRevenue), days.Sum(d => d.RefundedRevenue),
        days.Sum(d => d.GrossRevenue - d.RefundedRevenue),
        days.Sum(d => d.SoldTickets), days.Sum(d => d.RefundedTickets));
    private static decimal? Change(decimal current, decimal previous) =>
        previous == 0 ? null : Math.Round((current - previous) / previous * 100m, 2);
    private sealed record ConfirmedSale(Guid BookingId, decimal Amount, long Tickets);
    private sealed class Day(DateOnly date)
    {
        public DateOnly Date { get; } = date;
        public decimal GrossRevenue { get; set; }
        public decimal RefundedRevenue { get; set; }
        public long SoldTickets { get; set; }
        public long RefundedTickets { get; set; }
    }
}

public sealed record SalesSummary(decimal GrossRevenue, decimal RefundedRevenue, decimal NetRevenue,
    long SoldTickets, long RefundedTickets);
public sealed record DailySales(DateOnly Date, decimal GrossRevenue, decimal RefundedRevenue, decimal NetRevenue,
    long SoldTickets, long RefundedTickets);
public sealed record PreviousSalesPeriod(DateOnly From, DateOnly To, SalesSummary Summary);
public sealed record SalesChanges(decimal? NetRevenuePercent, decimal? SoldTicketsPercent);
public sealed record SalesStatisticsResponse(DateOnly From, DateOnly To, string Currency, string TimeZone,
    SalesSummary Summary, PreviousSalesPeriod PreviousPeriod, SalesChanges Changes, DailySales[] Daily);
