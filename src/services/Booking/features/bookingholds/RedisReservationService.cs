using StackExchange.Redis;

namespace Booking.Features.BookingHolds;

public sealed class RedisReservationService(IConnectionMultiplexer connection)
{
    private const string ReservationKeyPrefix = "reservation:";
    private readonly IDatabase _redis = connection.GetDatabase();

    public Task<bool> InitializeInventoryAsync(
        Guid eventId,
        Guid sectionId,
        int quantity) =>
        _redis.StringSetAsync(GetInventoryKey(eventId, sectionId), quantity, when: When.NotExists);

    public async Task<Guid?> TryReserveAsync(
        Guid eventId,
        Guid sectionId,
        int quantity,
        string customerId,
        DateTimeOffset expiresAt)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");

        var reservationId = Guid.NewGuid();
        var inventoryKey = GetInventoryKey(eventId, sectionId);
        var expirationIndexKey = GetReservationIndexKey(eventId, sectionId);
        var reservationKeyPrefix = ReservationKeyPrefix;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var expiration = expiresAt.ToUnixTimeMilliseconds();

        // Reap expired holds and decrement available stock in the same atomic operation
        // that attempts the new reservation.
        const string script = """
            local expiredIds = redis.call('ZRANGEBYSCORE', KEYS[2], '-inf', ARGV[1])
            for _, expiredId in ipairs(expiredIds) do
                local expiredKey = ARGV[5] .. expiredId
                local expiredQuantity = redis.call('HGET', expiredKey, 'quantity')
                if expiredQuantity then
                    redis.call('INCRBY', KEYS[1], tonumber(expiredQuantity))
                end
                redis.call('DEL', expiredKey)
                redis.call('ZREM', KEYS[2], expiredId)
            end

            local available = tonumber(redis.call('GET', KEYS[1]) or '-1')
            if available < 0 then
                return -2
            end
            if redis.call('EXISTS', ARGV[5] .. ARGV[2]) == 1 then
                return -1
            end
            if available < tonumber(ARGV[3]) then
                return 0
            end

            redis.call('DECRBY', KEYS[1], ARGV[3])
            redis.call('HSET', ARGV[5] .. ARGV[2],
                'eventId', ARGV[6],
                'sectionId', ARGV[7],
                'quantity', ARGV[3],
                'customerId', ARGV[4])
            redis.call('ZADD', KEYS[2], ARGV[8], ARGV[2])
            return 1
            """;

        var result = (long)await _redis.ScriptEvaluateAsync(
            script,
            [inventoryKey, expirationIndexKey],
            [
                now,
                reservationId.ToString("N"),
                quantity,
                customerId,
                reservationKeyPrefix,
                eventId.ToString("D"),
                sectionId.ToString("D"),
                expiration
            ]);

        return result == 1 ? reservationId : null;
    }

    private static RedisKey GetInventoryKey(Guid eventId, Guid sectionId) =>
        $"inventory:{eventId:N}:{sectionId:N}:available";

    private static RedisKey GetReservationIndexKey(Guid eventId, Guid sectionId) =>
        $"inventory:{eventId:N}:{sectionId:N}:reservations";
}
