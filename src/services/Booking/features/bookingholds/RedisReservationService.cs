using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Booking.Features.BookingHolds;

public sealed class RedisReservationService(
    IConnectionMultiplexer connection, IOptions<ReservationLimitOptions> options)
{
    private readonly IDatabase redis = connection.GetDatabase();
    private readonly ReservationLimitOptions limits = options.Value;
    private string DueKey => $"{limits.KeyPrefix}:due";

    public Task<bool> InitializeInventoryAsync(Guid eventId, Guid sectionId, int quantity) =>
        redis.StringSetAsync(GetInventoryKey(eventId, sectionId), quantity, when: When.NotExists);

    public async Task DeleteInventoryAsync(Guid eventId, Guid sectionId)
    {
        const string script = ReservationLua.Common + """
            local ids = redis.call('ZRANGE', KEYS[2], 0, -1)
            for _, id in ipairs(ids) do
                local key = 'reservation:' .. id
                releaseQuota(key, id)
                redis.call('DEL', key)
            end
            redis.call('DEL', KEYS[1], KEYS[2])
            return #ids
            """;
        await redis.ScriptEvaluateAsync(script, InventoryKeys(eventId, sectionId), [limits.KeyPrefix]);
    }

    public async Task<int> GetAvailableQuantityAsync(Guid eventId, Guid sectionId, int initialQuantity)
    {
        await InitializeInventoryAsync(eventId, sectionId, initialQuantity);
        const string script = ReservationLua.Common + """
            expireSection(KEYS[1], KEYS[2])
            return tonumber(redis.call('GET', KEYS[1]) or '-1')
            """;
        var result = (long)await redis.ScriptEvaluateAsync(script, InventoryKeys(eventId, sectionId), [limits.KeyPrefix]);
        if (result < 0) throw new InvalidOperationException("Redis inventory was not initialized correctly.");
        return checked((int)result);
    }

    public async Task<RedisReservationResult> TryReserveAsync(
        Guid reservationId, Guid eventId, Guid sectionId, int quantity, DateTimeOffset expiresAt, string clientKey)
    {
        if (reservationId == Guid.Empty) throw new ArgumentException("ReservationId must be a non-empty UUID.", nameof(reservationId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (string.IsNullOrWhiteSpace(clientKey)) throw new ArgumentException("A client identifier is required.", nameof(clientKey));
        const string script = ReservationLua.Common + """
            local id = ARGV[2]
            local quantity = tonumber(ARGV[3])
            local expiration = tonumber(ARGV[4])
            local maxReservations = tonumber(ARGV[7])
            local maxQuantity = tonumber(ARGV[8])
            if quantity > maxQuantity then return {-3, 0} end
            if expiration <= now then return {-4, 0} end
            expireSection(KEYS[1], KEYS[2])

            -- Quotas cover the whole event, including holds in other sections.
            local expired = redis.call('ZRANGEBYSCORE', KEYS[4], '-inf', now)
            for _, expiredId in ipairs(expired) do
                local key = 'reservation:' .. expiredId
                local inventory = redis.call('HGET', key, 'inventoryKey')
                local index = redis.call('HGET', key, 'indexKey')
                if inventory and index then expireReservation(expiredId, inventory, index) end
                redis.call('ZREM', KEYS[4], expiredId)
                redis.call('HDEL', KEYS[5], expiredId)
            end
            local entries = redis.call('ZRANGE', KEYS[4], 0, -1, 'WITHSCORES')
            local count = #entries / 2
            local total = 0
            for i = 1, #entries, 2 do
                total = total + tonumber(redis.call('HGET', KEYS[5], entries[i]) or '0')
            end
            local reservationLimit = count + 1 > maxReservations
            if reservationLimit or total + quantity > maxQuantity then
                local retry = 1
                for i = 1, #entries, 2 do
                    count = count - 1
                    total = total - tonumber(redis.call('HGET', KEYS[5], entries[i]) or '0')
                    retry = math.max(1, math.ceil((tonumber(entries[i + 1]) - now) / 1000))
                    if count + 1 <= maxReservations and total + quantity <= maxQuantity then break end
                end
                return {reservationLimit and 2 or 3, retry}
            end

            local available = tonumber(redis.call('GET', KEYS[1]) or '-1')
            if available < 0 then return {-2, 0} end
            local key = 'reservation:' .. id
            if redis.call('EXISTS', key) == 1 then return {-1, 0} end
            if available < quantity then return {0, 0} end
            redis.call('DECRBY', KEYS[1], quantity)
            redis.call('HSET', key, 'eventId', ARGV[5], 'sectionId', ARGV[6], 'quantity', quantity,
                'inventoryKey', KEYS[1], 'indexKey', KEYS[2], 'ownerKey', KEYS[6], 'dueKey', KEYS[3])
            redis.call('ZADD', KEYS[2], expiration, id)
            redis.call('ZADD', KEYS[3], expiration, id)
            redis.call('ZADD', KEYS[4], expiration, id)
            redis.call('HSET', KEYS[5], id, quantity)
            redis.call('HSET', KEYS[6], 'index', KEYS[4], 'quantities', KEYS[5])
            redis.call('PEXPIREAT', KEYS[6], expiration + 60000)
            local last = redis.call('ZRANGE', KEYS[4], -1, -1, 'WITHSCORES')
            redis.call('PEXPIREAT', KEYS[4], tonumber(last[2]) + 60000)
            redis.call('PEXPIREAT', KEYS[5], tonumber(last[2]) + 60000)
            return {1, 0}
            """;
        var prefix = $"{limits.KeyPrefix}:active:{clientKey}:{eventId:N}";
        var result = (RedisResult[])await redis.ScriptEvaluateAsync(script,
            [GetInventoryKey(eventId, sectionId), GetReservationIndexKey(eventId, sectionId), DueKey,
                prefix, $"{prefix}:quantities", $"{limits.KeyPrefix}:owner:{reservationId:N}"],
            [limits.KeyPrefix, reservationId.ToString("N"), quantity, expiresAt.ToUnixTimeMilliseconds(),
                eventId.ToString("D"), sectionId.ToString("D"), limits.MaxActiveReservationsPerEvent,
                limits.MaxReservedQuantityPerEvent]);
        var status = (int)result[0];
        if (status is -1 or -2 or -4) throw new ReservationControlUnavailableException();
        return new RedisReservationResult((RedisReservationStatus)status, (int)result[1]);
    }

    public async Task<ReservationFinalizationResult> ConfirmReservationAsync(Guid eventId, Guid sectionId, Guid reservationId)
    {
        const string script = ReservationLua.Common + """
            local id = ARGV[2]
            local key = 'reservation:' .. id
            local confirmed = 'reservation-confirmed:' .. id
            if redis.call('EXISTS', confirmed) == 1 then return 2 end
            local expiration = redis.call('ZSCORE', KEYS[2], id)
            if not expiration then
                if redis.call('EXISTS', 'reservation-expired:' .. id) == 1 then return -1 end
                return 0
            end
            if tonumber(expiration) <= now then
                expireReservation(id, KEYS[1], KEYS[2])
                return -1
            end
            local quantity = redis.call('HGET', key, 'quantity') or '0'
            releaseQuota(key, id)
            redis.call('DEL', key)
            redis.call('ZREM', KEYS[2], id)
            redis.call('HSET', confirmed, 'quantity', quantity)
            redis.call('EXPIRE', confirmed, 86400)
            return 1
            """;
        return ToFinalizationResult((long)await redis.ScriptEvaluateAsync(script,
            InventoryKeys(eventId, sectionId), [limits.KeyPrefix, reservationId.ToString("N")]));
    }

    public async Task RestoreConfirmedReservationAsync(Guid eventId, Guid sectionId, Guid reservationId)
    {
        const string script = """
            local confirmed = 'reservation-confirmed:' .. ARGV[1]
            if redis.call('EXISTS', confirmed) == 0 then return 0 end
            local quantity = tonumber(redis.call('HGET', confirmed, 'quantity') or '0')
            if quantity > 0 then redis.call('INCRBY', KEYS[1], quantity) end
            redis.call('DEL', confirmed)
            redis.call('SET', 'reservation-cancelled:' .. ARGV[1], '1', 'EX', 86400)
            return 1
            """;
        // Confirmation already removed the active quota. Compensation must not remove it again.
        await redis.ScriptEvaluateAsync(script, [GetInventoryKey(eventId, sectionId)], [reservationId.ToString("N")]);
    }

    public async Task<ReservationFinalizationResult> ReleaseReservationAsync(Guid eventId, Guid sectionId, Guid reservationId)
    {
        const string script = ReservationLua.Common + """
            local id = ARGV[2]
            local key = 'reservation:' .. id
            if redis.call('EXISTS', 'reservation-cancelled:' .. id) == 1 then return 2 end
            if redis.call('EXISTS', key) == 0 then
                if redis.call('EXISTS', 'reservation-expired:' .. id) == 1 then return -1 end
                return 0
            end
            local expiration = redis.call('ZSCORE', KEYS[2], id)
            if expiration and tonumber(expiration) <= now then
                expireReservation(id, KEYS[1], KEYS[2])
                return -1
            end
            local quantity = redis.call('HGET', key, 'quantity')
            if quantity then redis.call('INCRBY', KEYS[1], tonumber(quantity)) end
            releaseQuota(key, id)
            redis.call('DEL', key)
            redis.call('ZREM', KEYS[2], id)
            redis.call('SET', 'reservation-cancelled:' .. id, '1', 'EX', 86400)
            return 1
            """;
        return ToFinalizationResult((long)await redis.ScriptEvaluateAsync(script,
            InventoryKeys(eventId, sectionId), [limits.KeyPrefix, reservationId.ToString("N")]));
    }

    public async Task<int> CleanupExpiredReservationsAsync()
    {
        const string script = ReservationLua.Common + """
            local ids = redis.call('ZRANGEBYSCORE', KEYS[3], '-inf', now, 'LIMIT', 0, 100)
            for _, id in ipairs(ids) do
                local key = 'reservation:' .. id
                local inventory = redis.call('HGET', key, 'inventoryKey')
                local index = redis.call('HGET', key, 'indexKey')
                if inventory and index then expireReservation(id, inventory, index) end
                redis.call('ZREM', KEYS[3], id)
            end
            return #ids
            """;
        return (int)(long)await redis.ScriptEvaluateAsync(script, [DueKey, DueKey, DueKey], [limits.KeyPrefix]);
    }

    private RedisKey[] InventoryKeys(Guid eventId, Guid sectionId) =>
        [GetInventoryKey(eventId, sectionId), GetReservationIndexKey(eventId, sectionId), DueKey];
    private static RedisKey GetInventoryKey(Guid eventId, Guid sectionId) => $"inventory:{eventId:N}:{sectionId:N}:available";
    private static RedisKey GetReservationIndexKey(Guid eventId, Guid sectionId) => $"inventory:{eventId:N}:{sectionId:N}:reservations";
    private static ReservationFinalizationResult ToFinalizationResult(long result) => result switch
    {
        1 or 2 => ReservationFinalizationResult.Completed,
        -1 => ReservationFinalizationResult.Expired,
        _ => ReservationFinalizationResult.NotFound
    };
}

public enum RedisReservationStatus
{
    QuantityNotAllowed = -3,
    InsufficientInventory = 0,
    Reserved = 1,
    ActiveReservationLimitExceeded = 2,
    ReservedQuantityLimitExceeded = 3
}
public sealed record RedisReservationResult(RedisReservationStatus Status, int RetryAfterSeconds);
public enum ReservationFinalizationResult { Completed, Expired, NotFound }
