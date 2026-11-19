using TelemetryPlatform.Domain;

namespace TelemetryPlatform.Application;

public interface ITelemetryRepository
{
    Task<TelemetryReading?> GetLatestAsync(string machineId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TelemetryReading>> GetLatestAsync(IReadOnlyCollection<string> machineIds, CancellationToken cancellationToken = default);
    Task<TelemetryReading?> UpsertAsync(string machineId, DateTimeOffset observedAt, string payload, CancellationToken cancellationToken = default);
}

public interface IMachinePollTrackerRepository
{
    Task<DateTimeOffset?> GetLastPolledAtAsync(string machineId, CancellationToken cancellationToken = default);
    Task UpdateAsync(string machineId, DateTimeOffset polledAt, CancellationToken cancellationToken = default);
}

public interface IUnitOfWork : IAsyncDisposable
{
    ITelemetryRepository Telemetry { get; }
    IMachinePollTrackerRepository PollTracker { get; }
    Task BeginAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

public interface IDistributedLock
{
    Task<IAsyncDisposable?> AcquireAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default);
}

public interface ITelemetryNotifier
{
    Task NotifyAsync(string machineId, TelemetryReadingDto reading, CancellationToken cancellationToken = default);
}
