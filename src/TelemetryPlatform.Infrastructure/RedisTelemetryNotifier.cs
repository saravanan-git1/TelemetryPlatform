using System.Text.Json;
using StackExchange.Redis;
using TelemetryPlatform.Application;

namespace TelemetryPlatform.Infrastructure;

public sealed class RedisTelemetryNotifier : ITelemetryNotifier
{
    private readonly IConnectionMultiplexer _redis;
    public RedisTelemetryNotifier(IConnectionMultiplexer redis) => _redis = redis;

    public Task NotifyAsync(string machineId, TelemetryReadingDto reading, CancellationToken cancellationToken = default)
    {
        var sub = _redis.GetSubscriber();
        var json = JsonSerializer.Serialize(reading);
        return sub.PublishAsync(RedisChannel.Literal($"telemetry:{machineId}"), json);
    }
}
