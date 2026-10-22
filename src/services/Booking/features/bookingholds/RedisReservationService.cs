using StackExchange.Redis;

namespace Booking.Features.BookingHolds;

public sealed class RedisReservationService(IConnectionMultiplexer connection)
{
    private const string ReservationKeyPrefix = "reservation:";
    private const string ExpiredKeyPrefix = "reservation-expired:";
    private const string ConfirmedKeyPrefix = "reservation-confirmed:";
    private const string CancelledKeyPrefix = "reservation-cancelled:";
    private readonly IDatabase _redis = connection.GetDatabase();

    public Task<bool> InitializeInventoryAsync(Guid eventId, Guid sectionId, int quantity) =>
        _redis.StringSetAsync(GetInventoryKey(eventId, sectionId), quantity, when: When.NotExists);

    public async Task DeleteInventoryAsync(Guid eventId, Guid sectionId)
    {
        const string script = """
            local reservationIds = redis.call('ZRANGE', KEYS[2], 0, -1)
            for _, reservationId in ipairs(reservationIds) do
                redis.call('DEL', ARGV[1] .. reservationId)
            end
            redis.call('DEL', KEYS[1], KEYS[2])
            return #reservationIds
            """;

        await _redis.ScriptEvaluateAsync(
            script,
            [GetInventoryKey(eventId, sectionId), GetReservationIndexKey(eventId, sectionId)],
            [ReservationKeyPrefix]);
    }

    public async Task<long> ClearAllInventoryAsync()
    {
        long deletedKeyCount = 0;
        foreach (var endpoint in connection.GetEndPoints())
        {
            var server = connection.GetServer(endpoint);
            if (!server.IsConnected || server.IsReplica)
                continue;

            var keys = new[]
                {
                    "inventory:*",
                    "reservation:*",
                    "reservation-expired:*",
                    "reservation-confirmed:*",
                    "reservation-cancelled:*"
                }
                .SelectMany(pattern => server.Keys(database: _redis.Database, pattern: pattern))
                .Distinct()
                .ToArray();

            if (keys.Length > 0)
                deletedKeyCount += await _redis.KeyDeleteAsync(keys);
        }

        return deletedKeyCount;
    }

    public async Task<int> GetAvailableQuantityAsync(Guid eventId, Guid sectionId, int initialQuantity)
    {
        await InitializeInventoryAsync(eventId, sectionId, initialQuantity);

        const string script = """
            local expiredIds = redis.call('ZRANGEBYSCORE', KEYS[2], '-inf', ARGV[1])
            for _, expiredId in ipairs(expiredIds) do
                local expiredKey = ARGV[2] .. expiredId
                local expiredQuantity = redis.call('HGET', expiredKey, 'quantity')
                if expiredQuantity then redis.call('INCRBY', KEYS[1], tonumber(expiredQuantity)) end
                redis.call('DEL', expiredKey)
                redis.call('ZREM', KEYS[2], expiredId)
                redis.call('SET', ARGV[3] .. expiredId, '1', 'EX', 86400)
            end
            return tonumber(redis.call('GET', KEYS[1]) or '-1')
            """;

        var result = (long)await _redis.ScriptEvaluateAsync(
            script,
            [GetInventoryKey(eventId, sectionId), GetReservationIndexKey(eventId, sectionId)],
            [DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), ReservationKeyPrefix, ExpiredKeyPrefix]);

        if (result < 0)
            throw new InvalidOperationException("Redis inventory was not initialized correctly.");

        return checked((int)result);
    }

    public async Task<bool> TryReserveAsync(
        Guid reservationId,
        Guid eventId,
        Guid sectionId,
        int quantity,
        DateTimeOffset expiresAt)
    {
        if (reservationId == Guid.Empty)
            throw new ArgumentException("ReservationId must be a non-empty UUID.", nameof(reservationId));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");

        const string script = """
            local expiredIds = redis.call('ZRANGEBYSCORE', KEYS[2], '-inf', ARGV[1])
            for _, expiredId in ipairs(expiredIds) do
                local expiredKey = ARGV[4] .. expiredId
                local expiredQuantity = redis.call('HGET', expiredKey, 'quantity')
                if expiredQuantity then redis.call('INCRBY', KEYS[1], tonumber(expiredQuantity)) end
                redis.call('DEL', expiredKey)
                redis.call('ZREM', KEYS[2], expiredId)
                redis.call('SET', ARGV[8] .. expiredId, '1', 'EX', 86400)
            end

            local available = tonumber(redis.call('GET', KEYS[1]) or '-1')
            if available < 0 then return -2 end
            if redis.call('EXISTS', ARGV[4] .. ARGV[2]) == 1 then return -1 end
            if available < tonumber(ARGV[3]) then return 0 end

            redis.call('DECRBY', KEYS[1], ARGV[3])
            redis.call('HSET', ARGV[4] .. ARGV[2],
                'eventId', ARGV[5],
                'sectionId', ARGV[6],
                'quantity', ARGV[3])
            redis.call('ZADD', KEYS[2], ARGV[7], ARGV[2])
            return 1
            """;

        var result = (long)await _redis.ScriptEvaluateAsync(
            script,
            [GetInventoryKey(eventId, sectionId), GetReservationIndexKey(eventId, sectionId)],
            [
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                reservationId.ToString("N"),
                quantity,
                ReservationKeyPrefix,
                eventId.ToString("D"),
                sectionId.ToString("D"),
                expiresAt.ToUnixTimeMilliseconds(),
                ExpiredKeyPrefix
            ]);

        return result == 1;
    }

    public async Task<ReservationFinalizationResult> ConfirmReservationAsync(
        Guid eventId,
        Guid sectionId,
        Guid reservationId)
    {
        const string script = """
            local reservationKey = ARGV[1] .. ARGV[2]
            local confirmedKey = ARGV[3] .. ARGV[2]
            local expiredKey = ARGV[4] .. ARGV[2]
            if redis.call('EXISTS', confirmedKey) == 1 then return 2 end

            local expiration = redis.call('ZSCORE', KEYS[2], ARGV[2])
            if not expiration then
                if redis.call('EXISTS', expiredKey) == 1 then return -1 end
                return 0
            end
            if tonumber(expiration) <= tonumber(ARGV[5]) then
                local quantity = redis.call('HGET', reservationKey, 'quantity')
                if quantity then redis.call('INCRBY', KEYS[1], tonumber(quantity)) end
                redis.call('DEL', reservationKey)
                redis.call('ZREM', KEYS[2], ARGV[2])
                redis.call('SET', expiredKey, '1', 'EX', 86400)
                return -1
            end

            redis.call('DEL', reservationKey)
            redis.call('ZREM', KEYS[2], ARGV[2])
            redis.call('SET', confirmedKey, '1', 'EX', 86400)
            return 1
            """;

        var result = (long)await _redis.ScriptEvaluateAsync(
            script,
            [GetInventoryKey(eventId, sectionId), GetReservationIndexKey(eventId, sectionId)],
            [
                ReservationKeyPrefix,
                reservationId.ToString("N"),
                ConfirmedKeyPrefix,
                ExpiredKeyPrefix,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            ]);

        return ToFinalizationResult(result);
    }

    public async Task<ReservationFinalizationResult> ReleaseReservationAsync(
        Guid eventId,
        Guid sectionId,
        Guid reservationId)
    {
        const string script = """
            local reservationKey = ARGV[1] .. ARGV[2]
            local cancelledKey = ARGV[3] .. ARGV[2]
            local expiredKey = ARGV[4] .. ARGV[2]
            if redis.call('EXISTS', cancelledKey) == 1 then return 2 end
            if redis.call('EXISTS', reservationKey) == 0 then
                if redis.call('EXISTS', expiredKey) == 1 then return -1 end
                return 0
            end

            local expiration = redis.call('ZSCORE', KEYS[2], ARGV[2])
            local quantity = redis.call('HGET', reservationKey, 'quantity')
            if expiration and tonumber(expiration) <= tonumber(ARGV[5]) then
                if quantity then redis.call('INCRBY', KEYS[1], tonumber(quantity)) end
                redis.call('DEL', reservationKey)
                redis.call('ZREM', KEYS[2], ARGV[2])
                redis.call('SET', expiredKey, '1', 'EX', 86400)
                return -1
            end

            if quantity then redis.call('INCRBY', KEYS[1], tonumber(quantity)) end
            redis.call('DEL', reservationKey)
            redis.call('ZREM', KEYS[2], ARGV[2])
            redis.call('SET', cancelledKey, '1', 'EX', 86400)
            return 1
            """;

        var result = (long)await _redis.ScriptEvaluateAsync(
            script,
            [GetInventoryKey(eventId, sectionId), GetReservationIndexKey(eventId, sectionId)],
            [
                ReservationKeyPrefix,
                reservationId.ToString("N"),
                CancelledKeyPrefix,
                ExpiredKeyPrefix,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            ]);

        return ToFinalizationResult(result);
    }

    private static ReservationFinalizationResult ToFinalizationResult(long result) => result switch
    {
        1 or 2 => ReservationFinalizationResult.Completed,
        -1 => ReservationFinalizationResult.Expired,
        _ => ReservationFinalizationResult.NotFound
    };

    private static RedisKey GetInventoryKey(Guid eventId, Guid sectionId) =>
        $"inventory:{eventId:N}:{sectionId:N}:available";

    private static RedisKey GetReservationIndexKey(Guid eventId, Guid sectionId) =>
        $"inventory:{eventId:N}:{sectionId:N}:reservations";
}

public enum ReservationFinalizationResult
{
    Completed,
    Expired,
    NotFound
}
