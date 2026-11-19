using System.ComponentModel.DataAnnotations;

namespace Booking.Features.BookingHolds;

public sealed class ReservationLimitOptions
{
    [Range(1, 10000)] public int AttemptsPerWindow { get; set; } = 10;
    [Range(1, 86400)] public int WindowSeconds { get; set; } = 60;
    [Range(1, 1000)] public int MaxActiveReservationsPerEvent { get; set; } = 2;
    [Range(1, 100000)] public int MaxReservedQuantityPerEvent { get; set; } = 10;
    [Required, RegularExpression("^[0-9a-fA-F]{64}$")]
    public string HmacKey { get; set; } = "";
    [Required, StringLength(128), RegularExpression("^[a-zA-Z0-9:_-]+$")]
    public string KeyPrefix { get; set; } = "booking:reservation-limits:v1";
}
