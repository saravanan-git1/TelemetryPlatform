using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using TelemetryPlatform.Application;
using TelemetryPlatform.Api.Hubs;

namespace TelemetryPlatform.Api;

public sealed class RedisTelemetryBroadcastService : BackgroundService
{
    private readonly IHubContext<TelemetryHub> _hub;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisTelemetryBroadcastService> _logger;

    public RedisTelemetryBroadcastService(
        IHubContext<TelemetryHub> hub,
        IConnectionMultiplexer redis,
        ILogger<RedisTelemetryBroadcastService> logger)
    {
        _hub = hub;
        _redis = redis;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = _redis.GetSubscriber();
        await subscriber.SubscribeAsync(RedisChannel.Pattern("telemetry:*"), async (channel, value) =>
        {
            if (value.IsNull) return;
            try
            {
                var reading = JsonSerializer.Deserialize<TelemetryReadingDto>(value.ToString());
                if (reading is null) return;
                await _hub.Clients.Group(TelemetryHub.GroupName(reading.MachineId))
                    .SendAsync("TelemetryReading", reading, stoppingToken);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Ignoring malformed telemetry notification on {Channel}", channel);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Failed to broadcast telemetry notification on {Channel}", channel);
            }
        });
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
