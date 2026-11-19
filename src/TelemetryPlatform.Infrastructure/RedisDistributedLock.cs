using StackExchange.Redis;
using TelemetryPlatform.Application;

namespace TelemetryPlatform.Infrastructure;

public sealed class RedisDistributedLock : IDistributedLock
{
    private readonly IConnectionMultiplexer _redis;
    public RedisDistributedLock(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<IAsyncDisposable?> AcquireAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var token = Guid.NewGuid().ToString("N");
        if (!await db.StringSetAsync(key, token, expiration, When.NotExists)) return null;
        return new RedisLease(db, key, token);
    }

    private sealed class RedisLease(IDatabase db, RedisKey key, string token) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            const string script = "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";
            await db.ScriptEvaluateAsync(script, [key], [token]);
        }
    }
}
