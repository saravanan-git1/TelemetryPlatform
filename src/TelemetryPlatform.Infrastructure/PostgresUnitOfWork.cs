using System.Data;
using Dapper;
using Npgsql;
using TelemetryPlatform.Application;
using TelemetryPlatform.Domain;

namespace TelemetryPlatform.Infrastructure;

public sealed class PostgresUnitOfWork : IUnitOfWork
{
    private readonly NpgsqlConnection _connection;
    private NpgsqlTransaction? _transaction;

    public PostgresUnitOfWork(string connectionString)
    {
        _connection = new NpgsqlConnection(connectionString);
        Telemetry = new TelemetryRepository(() => _connection, () => _transaction);
        PollTracker = new MachinePollTrackerRepository(() => _connection, () => _transaction);
    }

    public ITelemetryRepository Telemetry { get; }
    public IMachinePollTrackerRepository PollTracker { get; }

    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        await _connection.OpenAsync(cancellationToken);
        _transaction = await _connection.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null) return;
        await _transaction.CommitAsync(cancellationToken);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null) return;
        await _transaction.RollbackAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null) await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

public sealed class TelemetryRepository : ITelemetryRepository
{
    private readonly Func<NpgsqlConnection> _connection;
    private readonly Func<NpgsqlTransaction?> _transaction;
    public TelemetryRepository(Func<NpgsqlConnection> connection, Func<NpgsqlTransaction?> transaction) { _connection = connection; _transaction = transaction; }

    public async Task<TelemetryReading?> GetLatestAsync(string machineId, CancellationToken cancellationToken = default)
    {
        var row = await _connection().QueryFirstOrDefaultAsync<TelemetryRow>(
            new CommandDefinition("""
                SELECT id, machine_id AS MachineId, observed_at AS ObservedAt,
                       payload::text AS Payload, created_at AS CreatedAt
                FROM telemetry_readings WHERE machine_id = @MachineId
                ORDER BY observed_at DESC LIMIT 1
                """, new { MachineId = machineId }, _transaction(), cancellationToken: cancellationToken));
        return row is null ? null : new TelemetryReading(row.Id, row.MachineId, row.ObservedAt, row.Payload, row.CreatedAt);
    }

    public async Task<IReadOnlyList<TelemetryReading>> GetLatestAsync(IReadOnlyCollection<string> machineIds, CancellationToken cancellationToken = default)
    {
        var rows = await _connection().QueryAsync<TelemetryRow>(
            new CommandDefinition("""
                SELECT DISTINCT ON (machine_id) id, machine_id AS MachineId, observed_at AS ObservedAt,
                       payload::text AS Payload, created_at AS CreatedAt
                FROM telemetry_readings
                WHERE machine_id = ANY(@MachineIds)
                ORDER BY machine_id, observed_at DESC
                """, new { MachineIds = machineIds.ToArray() }, _transaction(), cancellationToken: cancellationToken));
        return rows.Select(r => new TelemetryReading(r.Id, r.MachineId, r.ObservedAt, r.Payload, r.CreatedAt)).ToArray();
    }

    public async Task<TelemetryReading?> UpsertAsync(string machineId, DateTimeOffset observedAt, string payload, CancellationToken cancellationToken = default)
    {
        var row = await _connection().QueryFirstOrDefaultAsync<TelemetryRow>(
            new CommandDefinition("""
                INSERT INTO telemetry_readings (machine_id, observed_at, payload)
                VALUES (@MachineId, @ObservedAt, CAST(@Payload AS jsonb))
                ON CONFLICT (machine_id, observed_at) DO UPDATE
                SET payload = EXCLUDED.payload
                RETURNING id, machine_id AS MachineId, observed_at AS ObservedAt,
                          payload::text AS Payload, created_at AS CreatedAt
                """, new { MachineId = machineId, ObservedAt = observedAt, Payload = payload }, _transaction(), cancellationToken: cancellationToken));
        return row is null ? null : new TelemetryReading(row.Id, row.MachineId, row.ObservedAt, row.Payload, row.CreatedAt);
    }

    private sealed class TelemetryRow
    {
        public long Id { get; init; }
        public string MachineId { get; init; } = "";
        public DateTimeOffset ObservedAt { get; init; }
        public string Payload { get; init; } = "{}";
        public DateTimeOffset CreatedAt { get; init; }
    }
}

public sealed class MachinePollTrackerRepository : IMachinePollTrackerRepository
{
    private readonly Func<NpgsqlConnection> _connection;
    private readonly Func<NpgsqlTransaction?> _transaction;
    public MachinePollTrackerRepository(Func<NpgsqlConnection> connection, Func<NpgsqlTransaction?> transaction) { _connection = connection; _transaction = transaction; }

    public Task<DateTimeOffset?> GetLastPolledAtAsync(string machineId, CancellationToken cancellationToken = default) =>
        _connection().ExecuteScalarAsync<DateTimeOffset?>(
            new CommandDefinition("SELECT last_polled_at FROM machine_poll_tracker WHERE machine_id = @MachineId",
                new { MachineId = machineId }, _transaction(), cancellationToken: cancellationToken));

    public Task UpdateAsync(string machineId, DateTimeOffset polledAt, CancellationToken cancellationToken = default) =>
        _connection().ExecuteAsync(
            new CommandDefinition("""
                INSERT INTO machine_poll_tracker (machine_id, last_polled_at, updated_at)
                VALUES (@MachineId, @PolledAt, @PolledAt)
                ON CONFLICT (machine_id) DO UPDATE
                SET last_polled_at = EXCLUDED.last_polled_at,
                    updated_at = EXCLUDED.updated_at
                """, new { MachineId = machineId, PolledAt = polledAt }, _transaction(), cancellationToken: cancellationToken));
}
