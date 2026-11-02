namespace Booking.Features.Statistics;

public sealed record SalesPeriod(DateOnly From, DateOnly To)
{
    public const string TimeZoneId = "Europe/Stockholm";
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
    public int Days => To.DayNumber - From.DayNumber + 1;
    public SalesPeriod Previous => new(From.AddDays(-Days), From.AddDays(-1));
    public DateTime StartUtc => MidnightUtc(From);
    public DateTime EndUtc => MidnightUtc(To.AddDays(1));
    public static DateOnly LocalDate(DateTimeOffset timestamp) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timestamp, Zone).DateTime);

    public static SalesPeriod Create(DateOnly? from, DateOnly? to, DateTimeOffset now)
    {
        var end = to ?? LocalDate(now);
        var start = from ?? end.AddDays(-29);
        if (start > end || end.DayNumber - start.DayNumber >= 366
            || start.Year < 2 || end == DateOnly.MaxValue)
            throw new ArgumentException("Choose a valid date range of at most 366 days.");
        var period = new SalesPeriod(start, end);
        // Ensure the preceding comparison period can also be represented.
        if (start.DayNumber < period.Days)
            throw new ArgumentException("The comparison period is outside the supported date range.");
        return period;
    }

    private static DateTime MidnightUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Zone);
}
