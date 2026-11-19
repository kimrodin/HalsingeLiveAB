using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Booking.Features.BookingHolds;

public sealed class RedisReservationAttemptLimiter(
    IConnectionMultiplexer connection, IOptions<ReservationLimitOptions> options)
{
    private readonly ReservationLimitOptions limits = options.Value;

    public async Task<AttemptDecision> TryAcquireAsync(string clientKey)
    {
        const string script = """
            local time = redis.call('TIME')
            local now = tonumber(time[1]) * 1000 + math.floor(tonumber(time[2]) / 1000)
            local window = tonumber(ARGV[1]) * 1000
            redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', now - window)
            local count = redis.call('ZCARD', KEYS[1])
            if count >= tonumber(ARGV[2]) then
                local first = redis.call('ZRANGE', KEYS[1], 0, 0, 'WITHSCORES')
                return {0, math.max(1, math.ceil((tonumber(first[2]) + window - now) / 1000))}
            end
            redis.call('ZADD', KEYS[1], now, ARGV[3])
            redis.call('PEXPIRE', KEYS[1], window)
            if count + 1 == tonumber(ARGV[2]) then
                local first = redis.call('ZRANGE', KEYS[1], 0, 0, 'WITHSCORES')
                return {1, math.max(1, math.ceil((tonumber(first[2]) + window - now) / 1000))}
            end
            return {1, 0}
            """;
        var result = (RedisResult[])await connection.GetDatabase().ScriptEvaluateAsync(script,
            [$"{limits.KeyPrefix}:attempts:{clientKey}"],
            [limits.WindowSeconds, limits.AttemptsPerWindow, Guid.NewGuid().ToString("N")]);
        return new AttemptDecision((int)result[0] == 1, (int)result[1]);
    }
}

public sealed record AttemptDecision(bool Allowed, int RetryAfterSeconds);
