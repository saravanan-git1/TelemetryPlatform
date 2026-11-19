using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using TelemetryPlatform.Application;

namespace TelemetryPlatform.Worker;

public sealed class TelemetryWorker(
    ILogger<TelemetryWorker> logger,
    IOptions<TelemetryOptions> options,
    IServiceScopeFactory scopeFactory,
    IDistributedLock distributedLock,
    IHttpClientFactory httpClientFactory) : BackgroundService
{
    private readonly TelemetryOptions _options = options.Value;
    private readonly ResiliencePipeline<HttpResponseMessage> _retry = new ResiliencePipelineBuilder<HttpResponseMessage>()
        .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            OnRetry = args =>
            {
                args.Outcome.Result?.Dispose();
                return ValueTask.CompletedTask;
            },
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .HandleResult(response => (int)response.StatusCode >= 500)
        })
        .Build();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Clamp(_options.PollIntervalSeconds, 1, 3600));
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var (machineId, sourcePath) in _options.Machines)
                await PollMachineAsync(machineId, sourcePath, stoppingToken);
            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task PollMachineAsync(string machineId, string sourcePath, CancellationToken cancellationToken)
    {
        await using var lease = await distributedLock.AcquireAsync(
            $"telemetry:poll-lock:{machineId}", TimeSpan.FromMinutes(5), cancellationToken);
        if (lease is null) return;

        try
        {
            using var client = httpClientFactory.CreateClient("TelemetrySource");
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var url = new Uri(new Uri(_options.SourceUrl.TrimEnd('/') + "/"), sourcePath.TrimStart('/'));
            using var response = await _retry.ExecuteAsync(async token =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                return await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, token);
            }, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var payload = document.RootElement.GetRawText();
            var observedAt = GetObservedAt(document.RootElement) ?? DateTimeOffset.UtcNow;

            await using var scope = scopeFactory.CreateAsyncScope();
            await using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await uow.BeginAsync(cancellationToken);
            try
            {
                var reading = await uow.Telemetry.UpsertAsync(machineId, observedAt, payload, cancellationToken);
                await uow.PollTracker.UpdateAsync(machineId, DateTimeOffset.UtcNow, cancellationToken);
                await uow.CommitAsync(cancellationToken);
                if (reading is not null)
                {
                    var notifier = scope.ServiceProvider.GetRequiredService<ITelemetryNotifier>();
                    await notifier.NotifyAsync(machineId,
                        new TelemetryReadingDto(machineId, observedAt, document.RootElement.Clone()), cancellationToken);
                }
            }
            catch
            {
                await uow.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to poll telemetry for machine {MachineId}", machineId);
        }
    }

    private static DateTimeOffset? GetObservedAt(JsonElement root)
    {
        foreach (var name in new[] { "observedAt", "observed_at", "timestamp", "time" })
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value))
                if (value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var parsed))
                    return parsed;
        }
        return null;
    }
}
