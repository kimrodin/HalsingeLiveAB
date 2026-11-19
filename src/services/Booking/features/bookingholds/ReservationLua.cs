namespace Booking.Features.BookingHolds;

// Shared transitions keep stock, client quotas and orphan cleanup consistent.
// As with the existing inventory scripts, this requires a single Redis database (not Cluster).
internal static class ReservationLua
{
    public const string Common = """
        local namespace = ARGV[1]
        local time = redis.call('TIME')
        local now = tonumber(time[1]) * 1000 + math.floor(tonumber(time[2]) / 1000)
        local function releaseQuota(reservationKey, id)
            local owner = redis.call('HGET', reservationKey, 'ownerKey')
            if owner then
                local index = redis.call('HGET', owner, 'index')
                local quantities = redis.call('HGET', owner, 'quantities')
                if index and quantities then
                    redis.call('ZREM', index, id)
                    redis.call('HDEL', quantities, id)
                    if redis.call('ZCARD', index) == 0 then redis.call('DEL', index, quantities) end
                end
                redis.call('DEL', owner)
            end
            local due = redis.call('HGET', reservationKey, 'dueKey')
            if due then redis.call('ZREM', due, id) end
        end
        local function expireReservation(id, inventory, index)
            local key = 'reservation:' .. id
            local quantity = redis.call('HGET', key, 'quantity')
            if quantity then redis.call('INCRBY', inventory, tonumber(quantity)) end
            releaseQuota(key, id)
            redis.call('DEL', key)
            redis.call('ZREM', index, id)
            redis.call('SET', 'reservation-expired:' .. id, '1', 'EX', 86400)
        end
        local function expireSection(inventory, index)
            local ids = redis.call('ZRANGEBYSCORE', index, '-inf', now)
            for _, id in ipairs(ids) do expireReservation(id, inventory, index) end
        end

        """;
}
